using TabataTimer.Models;

namespace TabataTimer.Services.Interfaces;

public interface ISettingsManager
{
    AppSettings Load();
    void Save(AppSettings settings);
}
