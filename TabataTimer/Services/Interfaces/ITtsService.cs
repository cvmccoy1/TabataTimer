using Windows.Media.SpeechSynthesis;

namespace TabataTimer.Services.Interfaces;

public interface ITtsService : IDisposable
{
    void SetVolume(double volume);
    void SetVoice(string? voiceName);
    Task Speak(string text);
    void Stop();
    IReadOnlyList<VoiceInformation> GetAvailableVoices();
}
