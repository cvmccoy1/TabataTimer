namespace TabataTimer.Services;

public interface IAudioService : IDisposable
{
    void SetVolume(double volume);
    void PlayWarningBeep();
    void PlayPhaseEndBeep();
    void PlayFinalBeep();
}
