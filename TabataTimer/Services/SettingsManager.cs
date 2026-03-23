using Newtonsoft.Json;
using TabataTimer.Models;

namespace TabataTimer.Services
{
    public static class SettingsManager
    {
        private static readonly string AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TabataTimer");

        private static readonly string SettingsFile = Path.Combine(AppDataPath, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (!Directory.Exists(AppDataPath))
                    Directory.CreateDirectory(AppDataPath);

                if (!File.Exists(SettingsFile))
                    return new AppSettings();

                var json = File.ReadAllText(SettingsFile);
                return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                if (!Directory.Exists(AppDataPath))
                    Directory.CreateDirectory(AppDataPath);

                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(SettingsFile, json);
            }
            catch
            {
                // Silently fail — don't crash the app on save error
            }
        }
    }
}
