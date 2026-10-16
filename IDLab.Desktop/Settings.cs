using System;
using System.Collections.Generic;
using System.IO;

namespace IDLab.Desktop
{
    /// <summary>
    /// Per-machine preferences (theme, graphics quality) in %LOCALAPPDATA%\IDLab\ayarlar.txt as "key=value" lines.
    /// Failing to read or write is never an error: the defaults are used.
    /// </summary>
    internal static class Settings
    {
        private static readonly Dictionary<string, string> Values = new Dictionary<string, string>();

        private static string FilePath
        {
            get
            {
                string dir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(dir)) dir = Path.GetTempPath();
                return Path.Combine(dir, "IDLab", "ayarlar.txt");
            }
        }

        public static void Load()
        {
            try
            {
                string[] lines;
                if (Platform.LoadSettingsText != null) lines = (Platform.LoadSettingsText() ?? "").Split('\n');
                else if (!File.Exists(FilePath)) return;
                else lines = File.ReadAllLines(FilePath);
                foreach (var line in lines)
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0) Values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
            }
            catch (Exception)
            {
                // Defaults.
            }
        }

        public static string Get(string key, string fallback) => Values.TryGetValue(key, out var v) ? v : fallback;

        /// <summary>Changes a value for this session only (command-line overrides).</summary>
        public static void Override(string key, string value) => Values[key] = value;

        public static void Set(string key, string value)
        {
            Values[key] = value;
            try
            {
                var lines = new List<string>();
                foreach (var kv in Values) lines.Add(kv.Key + "=" + kv.Value);
                if (Platform.SaveSettingsText != null)
                {
                    Platform.SaveSettingsText(string.Join("\n", lines));
                    return;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllLines(FilePath, lines);
            }
            catch (Exception)
            {
                // Not saved; still applied for this session.
            }
        }
    }
}
