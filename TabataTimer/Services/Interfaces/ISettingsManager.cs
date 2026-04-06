using TabataTimer.Models;

namespace TabataTimer.Services;

public interface ISettingsManager
{
    AppSettings Load();
    void Save(AppSettings settings);
}
