using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Launcher.Settings
{
    /// <summary>
    /// Loads and saves <see cref="AppSettings"/> as JSON under the user's local app data folder.
    /// </summary>
    internal static class SettingsService
    {
        private static readonly string SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Launcher");

        private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings is not null)
                    {
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load settings: {ex.Message}");
            }

            return new AppSettings();
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                Directory.CreateDirectory(SettingsDirectory);
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }
    }
}
