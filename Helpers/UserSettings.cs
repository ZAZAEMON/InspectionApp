using System.IO;

namespace InspectionApp.Helpers
{
    /// <summary>
    /// Tiny persistent settings store. Saves a key=value text file
    /// next to the EXE so user preferences survive app restarts.
    /// File: &lt;exe folder&gt;\app_settings.txt
    /// </summary>
    public static class UserSettings
    {
        private static string SettingsFile =>
            Path.Combine(AppContext.BaseDirectory, "app_settings.txt");

        // ----- KEY: Excel save folder ---------------------------------------
        // Set via the "Set Save Path" button in Create Part Type. When set,
        // every Submit on the Home screen saves there silently. When NOT set
        // (or the folder no longer exists), the app falls back to the
        // InspectionReports folder next to the EXE.

        public static string? GetSavePath()
        {
            var path = Read("SavePath");
            if (string.IsNullOrWhiteSpace(path)) return null;
            return Directory.Exists(path) ? path : null;
        }

        public static void SetSavePath(string path) => Write("SavePath", path);

        public static void ClearSavePath() => Write("SavePath", "");

        // ----- low-level read / write --------------------------------------

        private static Dictionary<string, string> ReadAll()
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(SettingsFile)) return dict;
            try
            {
                foreach (var line in File.ReadAllLines(SettingsFile))
                {
                    var idx = line.IndexOf('=');
                    if (idx <= 0) continue;
                    dict[line.Substring(0, idx).Trim()] = line.Substring(idx + 1).Trim();
                }
            }
            catch { /* corrupt or unreadable — treat as empty */ }
            return dict;
        }

        private static string? Read(string key)
        {
            var dict = ReadAll();
            return dict.TryGetValue(key, out var v) ? v : null;
        }

        private static void Write(string key, string value)
        {
            try
            {
                var dict = ReadAll();
                dict[key] = value;
                File.WriteAllLines(SettingsFile,
                    dict.Select(kv => $"{kv.Key}={kv.Value}"));
            }
            catch { /* swallow — settings are non-critical */ }
        }
    }
}
