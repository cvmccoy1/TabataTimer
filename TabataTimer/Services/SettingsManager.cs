using System.IO;
using Newtonsoft.Json;
using TabataTimer.Models;
using TabataTimer.Services.Interfaces;

namespace TabataTimer.Services;

public class SettingsManager : ISettingsManager
{
    private readonly string _settingsPath;

    public static SettingsManager Instance { get; private set; } = null!;

    public SettingsManager(string settingsPath)
    {
        _settingsPath = settingsPath;
        Instance = this;
    }

    public AppSettings Load()
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (!File.Exists(_settingsPath))
                return new AppSettings();

            var json = File.ReadAllText(_settingsPath);
            return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
        }
    }
}
