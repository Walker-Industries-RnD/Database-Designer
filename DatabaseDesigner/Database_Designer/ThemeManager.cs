using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace Database_Designer
{


    public static class ThemeManager
    {
        public class ThemeConfig
        {
            public Dictionary<string, string> Colors { get; set; } = new();
            public string BackgroundImage { get; set; }
        }

        public static string CurrentBackgroundImage { get; private set; }
        public static string CurrentThemeName { get; private set; }

        public static string ThemesRoot(string userFolder) => Path.Combine(userFolder, "Themes");

        public static Color? GetPrimarySwatch(string userFolder, string themeName)
        {
            try
            {
                if (string.IsNullOrEmpty(themeName) || themeName == "Default") return null;

                var configPath = Path.Combine(ThemesRoot(userFolder), themeName, "theme.json");
                if (!File.Exists(configPath)) return null;

                var config = JsonSerializer.Deserialize<ThemeConfig>(File.ReadAllText(configPath));
                if (config?.Colors == null || config.Colors.Count == 0) return null;

                if (config.Colors.TryGetValue("Theme_BackgroundColor", out var bg) && TryParseColor(bg, out var bgColor))
                    return bgColor;
                foreach (var kv in config.Colors)
                    if (TryParseColor(kv.Value, out var c)) return c;
            }
            catch { }
            return null;
        }

        public static List<string> AvailableThemes(string userFolder)
        {
            var root = ThemesRoot(userFolder);
            var list = new List<string> { "Default" };
            try
            {
                if (Directory.Exists(root))
                    list.AddRange(Directory.GetDirectories(root)
                        .Where(d => File.Exists(Path.Combine(d, "theme.json")))
                        .Select(Path.GetFileName));
            }
            catch { }
            return list;
        }

 
        private static void SetThemeColor(string key, string hex)
        {
            try
            {
                if (!TryParseColor(hex, out var color))
                {
                    Console.WriteLine($"[ThemeManager] Ignoring '{key}': '{hex}' is not #RRGGBB or #AARRGGBB");
                    return;
                }
                if (key.EndsWith("Brush", StringComparison.Ordinal))
                    Application.Current.Resources[key] = new SolidColorBrush(color);
                else
                    Application.Current.Resources[key] = color;
            }
            catch { }
        }

        public static Brush ResolveBrush(string key, Color fallback)
        {
            try
            {
                var value = Application.Current.Resources[key];
                if (value is SolidColorBrush brush) return brush;
                if (value is Color color) return new SolidColorBrush(color);
            }

            catch { }
            return new SolidColorBrush(fallback);
        }

        private static bool TryParseColor(string hex, out Color color)
        {
            color = Colors.Black;
            if (string.IsNullOrWhiteSpace(hex)) return false;
            hex = hex.Trim().TrimStart('#');
            try
            {
                if (hex.Length == 6)
                {
                    color = Color.FromArgb(0xFF,
                        Convert.ToByte(hex.Substring(0, 2), 16),
                        Convert.ToByte(hex.Substring(2, 2), 16),
                        Convert.ToByte(hex.Substring(4, 2), 16));
                    return true;
                }
                if (hex.Length == 8)
                {
                    color = Color.FromArgb(
                        Convert.ToByte(hex.Substring(0, 2), 16),
                        Convert.ToByte(hex.Substring(2, 2), 16),
                        Convert.ToByte(hex.Substring(4, 2), 16),
                        Convert.ToByte(hex.Substring(6, 2), 16));
                    return true;
                }
            }
            catch { }
            return false;
        }


        public static void InstallStarterThemes(string userFolder)
        {
            try
            {
                try
                {
                    var stale = Path.Combine(ThemesRoot(userFolder), "Default", "theme.json");
                    if (File.Exists(stale)) File.Delete(stale);
                }
                catch { }

                Write(userFolder, "Y2K Chrome", new ThemeConfig
                {
                    Colors = new()
                    {
                        ["Theme_BackgroundColor"] = "#1D1A6B",
                        ["Theme_TextOnPrimaryColor"] = "#E6F4FF",
                        ["Theme_TextBrush"] = "#161257"
                    },
                    BackgroundImage = "background.jpg"
                }, ThemeAssets.Y2KBackgroundJpgBase64);

                Write(userFolder, "Midnight", new ThemeConfig
                {
                    Colors = new()
                    {
                        ["Theme_BackgroundColor"] = "#0E0E12",
                        ["Theme_TextOnPrimaryColor"] = "#E6E6F0"
                    }
                });
                Write(userFolder, "Sandstone", new ThemeConfig
                {
                    Colors = new()
                    {
                        ["Theme_BackgroundColor"] = "#2C2B28",
                        ["Theme_TextOnPrimaryColor"] = "#F0E9D2"
                    }
                });
            }
            catch { }
        }

        private static readonly Dictionary<string, object> _baseline = new();

        private static void RestoreBaseline()
        {
            foreach (var kv in _baseline)
            {
                try
                {
                    if (kv.Value == null) Application.Current.Resources.Remove(kv.Key);
                    else Application.Current.Resources[kv.Key] = kv.Value;
                }
                catch { }
            }
        }

        public static void ResetToBaseline()
        {
            RestoreBaseline();
            CurrentThemeName = "Default";
            CurrentBackgroundImage = null;
        }

        private static void RememberBaseline(string key)
        {
            if (_baseline.ContainsKey(key)) return;
            try
            {
                _baseline[key] = Application.Current.Resources.Contains(key)
                    ? Application.Current.Resources[key]
                    : null;
            }
            catch { _baseline[key] = null; }
        }

        public static void Apply(string userFolder, string themeName)
        {
            CurrentThemeName = string.IsNullOrEmpty(themeName) ? "Default" : themeName;
            CurrentBackgroundImage = null;

            RestoreBaseline();

            try
            {
                var folder = Path.Combine(ThemesRoot(userFolder), CurrentThemeName);
                var configPath = Path.Combine(folder, "theme.json");
                if (!File.Exists(configPath)) return;

                var config = JsonSerializer.Deserialize<ThemeConfig>(File.ReadAllText(configPath));
                if (config == null) return;

                foreach (var kv in config.Colors ?? new())
                {
                    RememberBaseline(kv.Key);
                    SetThemeColor(kv.Key, kv.Value);
                }

                if (!string.IsNullOrEmpty(config.BackgroundImage))
                {
                    var img = Path.Combine(folder, config.BackgroundImage);
                    if (File.Exists(img)) CurrentBackgroundImage = img;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ThemeManager] Apply failed: {ex.Message}");
            }
        }


        private static string DefaultFile(string userFolder) => Path.Combine(userFolder, "theme-default.txt");

        public static string GetDefault(string userFolder)
        {
            try { return File.Exists(DefaultFile(userFolder)) ? File.ReadAllText(DefaultFile(userFolder)).Trim() : "Default"; }
            catch { return "Default"; }
        }

        public static void SetDefault(string userFolder, string themeName)
        {
            try { File.WriteAllText(DefaultFile(userFolder), themeName ?? "Default"); }
            catch { }
        }


        private static void Write(string userFolder, string name, ThemeConfig config, string backgroundJpgBase64 = null, bool force = false)
        {
            var folder = Path.Combine(ThemesRoot(userFolder), name);
            if (Directory.Exists(folder) && !force) return;
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "theme.json"),
                JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));

            if (!string.IsNullOrEmpty(backgroundJpgBase64) && !string.IsNullOrEmpty(config.BackgroundImage))
            {
                try { File.WriteAllBytes(Path.Combine(folder, config.BackgroundImage), Convert.FromBase64String(backgroundJpgBase64)); }
                catch { }
            }
        }
    }
}
