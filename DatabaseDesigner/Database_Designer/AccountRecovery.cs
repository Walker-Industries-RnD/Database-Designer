using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Pariah_Cybersecurity;
using Walker.Crypto;
using WISecureData;

namespace Database_Designer
{
    public static class AccountRecovery
    {
        private const string VaultFile = "recovery.vault";
        private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTVWXYZ23456789";

        public static string PendingCodeToShow { get; set; }

        private static string Fresh(string s) => s == null ? null : new string(s.AsSpan());

        public static string DataDir => Path.Combine(AppContext.BaseDirectory, "DatabaseDesignerData");

        private static string VaultPath(string dataDir, string username) => Path.Combine(dataDir, username, VaultFile);

        public static bool HasVault(string dataDir, string username) => File.Exists(VaultPath(dataDir, username));

        public static string GenerateCode()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 25; i++)
            {
                if (i > 0 && i % 5 == 0) sb.Append('-');
                sb.Append(CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)]);
            }
            return sb.ToString();
        }

        public static string NormalizeCode(string code) =>
            new string((code ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

        public static async Task WriteVault(string dataDir, string username, string password, string code)
        {
            var folder = Path.Combine(dataDir, username);
            Directory.CreateDirectory(folder);
            var enc = await AsyncAESEncryption.EncryptAsync(Fresh(password), Fresh(NormalizeCode(code)).ToSecureData());
            var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(enc.ToString()));
            var tmp = VaultPath(dataDir, username) + ".tmp";
            await File.WriteAllTextAsync(tmp, payload);
            File.Move(tmp, VaultPath(dataDir, username), overwrite: true);
        }

        public static async Task<string> OpenVault(string dataDir, string username, string code)
        {
            if (!HasVault(dataDir, username))
                throw new InvalidOperationException("This account has no recovery code set up yet.");
            try
            {
                var payload = await File.ReadAllTextAsync(VaultPath(dataDir, username));
                var aes = SimpleAESEncryption.AESEncryptedText.FromString(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
                return await AsyncAESEncryption.DecryptAsync(aes, Fresh(NormalizeCode(code)).ToSecureData());
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                throw new UnauthorizedAccessException("That recovery code isn't right for this account.");
            }
        }

        public static async Task EnsureVaultAfterLogin(string dataDir, string username, string password)
        {
            try
            {
                if (HasVault(dataDir, username)) return;
                var code = GenerateCode();
                await WriteVault(dataDir, username, password, code);
                PendingCodeToShow = code;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AccountRecovery] Could not create a recovery code: {ex.Message}");
            }
        }

        public static string ValidateNewPassword(string password, string confirm)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 8) return "Use at least 8 characters.";
            if (password != confirm) return "The passwords don't match.";
            return null;
        }

        public static async Task<string> ResetWithCode(string dataDir, string username, string code, string newPassword, Action<string> progress = null)
        {
            progress?.Invoke("Checking your recovery code…");
            var oldPassword = await OpenVault(dataDir, username, code);
            return await ChangePassword(dataDir, username, oldPassword, newPassword, progress);
        }

        public static async Task<string> ChangePassword(string dataDir, string username, string oldPassword, string newPassword, Action<string> progress = null)
        {
            var acs = new DataHandler.AccountsWithSessions();
            var usersJson = Path.Combine(dataDir, "Users.json");
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var backupDir = Path.Combine(dataDir, username, "Backups", "password-change-" + stamp);

            progress?.Invoke("Signing in with the current password…");
            SecureData oldKey;
            DataHandler.AccountsWithSessions.ConnectedSessionReturn oldSession;
            try
            {
                (oldKey, oldSession) = await acs.LoginUser(Fresh(username), Fresh(dataDir), Fresh(oldPassword).ToSecureData(), true);
            }
            catch (Exception ex)
            {
                throw new UnauthorizedAccessException("The current password isn't right. " + ex.Message);
            }
            var oldKeyText = oldKey.ConvertToString();
            Directory.CreateDirectory(backupDir);
            File.Copy(usersJson, Path.Combine(backupDir, "Users.json"), overwrite: true);

            progress?.Invoke("Unlocking your projects…");
            var projects = new List<(string path, string original, string plain)>();
            var projectsRoot = Path.Combine(dataDir, username, "Projects");
            if (Directory.Exists(projectsRoot))
            {
                foreach (var file in Directory.GetFiles(projectsRoot, "*.secdbdesign", SearchOption.AllDirectories))
                {
                    var original = await File.ReadAllTextAsync(file);
                    var aes = SimpleAESEncryption.AESEncryptedText.FromString(Encoding.UTF8.GetString(Convert.FromBase64String(original)));
                    var plain = await AsyncAESEncryption.DecryptAsync(aes, Fresh(oldKeyText).ToSecureData());
                    projects.Add((file, original, plain));
                }
            }
            foreach (var (path, original, _) in projects)
            {
                var rel = Path.GetRelativePath(projectsRoot, path);
                var dest = Path.Combine(backupDir, "Projects", rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                await File.WriteAllTextAsync(dest, original);
            }

            bool accountRemoved = false;
            try
            {
                progress?.Invoke("Updating your account…");
                await acs.RemoveAccount(oldSession, Fresh(oldKeyText).ToSecureData());
                accountRemoved = true;
                await acs.CreateUser(Fresh(username), Fresh(newPassword).ToSecureData(), Fresh(dataDir));
                var (newKey, newSession) = await acs.LoginUser(Fresh(username), Fresh(dataDir), Fresh(newPassword).ToSecureData(), true);
                var newKeyText = newKey.ConvertToString();

                progress?.Invoke($"Re-encrypting {projects.Count} project(s)…");
                foreach (var (path, _, plain) in projects)
                {
                    var enc = await AsyncAESEncryption.EncryptAsync(plain, Fresh(newKeyText).ToSecureData());
                    var tmp = path + ".tmp";
                    await File.WriteAllTextAsync(tmp, Convert.ToBase64String(Encoding.UTF8.GetBytes(enc.ToString())));
                    File.Move(tmp, path, overwrite: true);
                }
                try { await acs.LogoutUser(newSession, Fresh(newKeyText).ToSecureData()); } catch { }
            }
            catch (Exception ex)
            {
                progress?.Invoke("Something went wrong; restoring your account…");
                if (accountRemoved) File.Copy(Path.Combine(backupDir, "Users.json"), usersJson, overwrite: true);
                foreach (var (path, original, _) in projects)
                {
                    try { await File.WriteAllTextAsync(path, original); File.Delete(path + ".tmp"); } catch { }
                }
                throw new InvalidOperationException("Your password was not changed (everything was restored): " + ex.Message, ex);
            }

            var code = GenerateCode();
            await WriteVault(dataDir, username, newPassword, code);
            progress?.Invoke("Done.");
            return code;
        }
    }
}
