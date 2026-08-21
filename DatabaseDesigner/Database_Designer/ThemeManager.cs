using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace Database_Designer
{


    //If you can see this, I got some help from Claude to put some notes down! (Trust me the ones I originally set were BAD)


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

        // Best-effort representative color for a theme, used for the little
        // swatch in the theme picker. Returns null for "Default"/unknown so the
        // caller can fall back to a neutral tile.
        public static Color? GetPrimarySwatch(string userFolder, string themeName)
        {
            try
            {
                if (string.IsNullOrEmpty(themeName) || themeName == "Default") return null;

                var configPath = Path.Combine(ThemesRoot(userFolder), themeName, "theme.json");
                if (!File.Exists(configPath)) return null;

                var config = JsonSerializer.Deserialize<ThemeConfig>(File.ReadAllText(configPath));
                if (config?.Colors == null || config.Colors.Count == 0) return null;

                // Prefer the background color, then any parseable color.
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

 

        // The resource TYPE must match how the app consumes the key:
        //   *Color keys  -> Color   (XAML: <SolidColorBrush Color="{DynamicResource
        //                            Theme_BackgroundColor}"/>, 134 bindings; C#
        //                            reads them via ResolveBrush, which wraps a Color)
        //   *Brush keys  -> Brush   (XAML: Foreground="{DynamicResource Theme_TextBrush}")
        // Storing the wrong type breaks every binding that uses the key — a Brush
        // can't be assigned to a Color property, and vice versa.
        private static void SetThemeColor(string key, string hex)
        {
            try
            {
                if (!TryParseColor(hex, out var color)) return;
                if (key.EndsWith("Brush", StringComparison.Ordinal))
                    Application.Current.Resources[key] = new SolidColorBrush(color);
                else
                    Application.Current.Resources[key] = color;
            }
            catch { }
        }

        // Safe resource -> Brush resolver for code that used to hard-cast
        // Application.Current.Resources[key] to (SolidColorBrush). Never throws,
        // so a missing or mistyped theme resource can't crash window creation.
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


        // Installs the built-in themes users can apply or copy and edit. These are
        // opt-in — the app ships on "Default" (App.xaml baseline). "Y2K Chrome" is
        // electric indigo + icy chrome, with a bundled chrome/aqua wallpaper.
        // Theme_TextBrush is a *Brush key; the
        // others are *Color keys — SetThemeColor stores the matching type.

        public static void InstallStarterThemes(string userFolder)
        {
            try
            {
                // "Default" is the App.xaml baseline (clean white) and has no
                // theme.json — Apply() resolves it to the baseline when no file is
                // present. Older builds force-wrote a dark "Default" theme.json that
                // was byte-identical to "Sandstone"; remove any such stale file so
                // Default reverts to the white baseline.
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

        public static void Apply(string userFolder, string themeName)
        {
            CurrentThemeName = themeName;
            CurrentBackgroundImage = null;

            if (string.IsNullOrEmpty(themeName))
                return;

            // "Default" now resolves to an installed blended theme.json if present
            // (see InstallStarterThemes). If it hasn't been installed yet — e.g. an
            // older user folder — fall back to the App.xaml baseline, same as before.
            if (themeName == "Default" && !File.Exists(Path.Combine(ThemesRoot(userFolder), "Default", "theme.json")))
                return;

            try
            {
                var folder = Path.Combine(ThemesRoot(userFolder), themeName);
                var configPath = Path.Combine(folder, "theme.json");
                if (!File.Exists(configPath)) return;

                var config = JsonSerializer.Deserialize<ThemeConfig>(File.ReadAllText(configPath));
                if (config == null) return;

                foreach (var kv in config.Colors)
                    SetThemeColor(kv.Key, kv.Value);

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

        // "Default" is the shipped default look (the App.xaml baseline). Y2K Chrome
        // and the others are installed by InstallStarterThemes but are opt-in; a
        // user who explicitly picks a theme gets it saved to theme-default.txt and
        // that wins on next launch.
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

            // Materialize a bundled background image (shipped base64) into the
            // theme folder so Apply() can find it on disk.
            if (!string.IsNullOrEmpty(backgroundJpgBase64) && !string.IsNullOrEmpty(config.BackgroundImage))
            {
                try { File.WriteAllBytes(Path.Combine(folder, config.BackgroundImage), Convert.FromBase64String(backgroundJpgBase64)); }
                catch { }
            }
        }
    }
}
