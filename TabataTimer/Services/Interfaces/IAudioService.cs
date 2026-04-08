namespace TabataTimer.Services.Interfaces;

public interface IAudioService : IDisposable
{
    void SetVolume(double volume);
    void PlayWarningBeep();
    void PlayPhaseEndBeep();
    void PlayFinalBeep();
    void PlayMidWorkBeep();
}
