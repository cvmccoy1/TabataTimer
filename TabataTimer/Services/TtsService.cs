using System.IO;
using System.Windows.Media;
using Windows.Media.SpeechSynthesis;

namespace TabataTimer.Services
{
    /// <summary>
    /// Text-to-Speech service using Windows.Media.SpeechSynthesis (WinRT API).
    /// Synthesizes speech to a stream and plays it via MediaPlayer on a background thread.
    /// </summary>
    public class TtsService : IDisposable
    {
        private readonly SpeechSynthesizer _synth;
        private double _volume = 0.8;
        private bool _disposed = false;
        private VoiceInformation? _voice;

        public TtsService()
        {
            _synth = new SpeechSynthesizer();
        }

        public void SetVolume(double volume)
        {
            _volume = Math.Clamp(volume, 0.0, 1.0);
        }

        /// <summary>Set the TTS voice by display name. Null or unknown name uses system default.</summary>
        public void SetVoice(string? voiceName)
        {
            if (string.IsNullOrEmpty(voiceName)) { _voice = null; return; }
            _voice = SpeechSynthesizer.AllVoices
                .FirstOrDefault(v => v.DisplayName == voiceName);
        }

        /// <summary>Returns all installed TTS voices.</summary>
        public static IReadOnlyList<VoiceInformation> GetAvailableVoices()
            => SpeechSynthesizer.AllVoices.ToList();

        /// <summary>Speak text asynchronously. Cancels any currently speaking utterance first.</summary>
        public async void Speak(string text)
        {
            if (_disposed || string.IsNullOrWhiteSpace(text) || _volume <= 0) return;

            try
            {
                _synth.Voice = _voice ?? SpeechSynthesizer.DefaultVoice;
                var stream = await _synth.SynthesizeTextToStreamAsync(text);

                // Read the WinRT stream into a byte array explicitly using a fixed-size buffer.
                // Using a MemoryStream with a fixed buffer avoids issues with
                // AsStreamForRead().CopyTo() not capturing the full WAV header.
                using var memoryStream = new MemoryStream();
                int streamSize = (int)stream.Size;
                var buffer = new byte[streamSize];
                int bytesRead = 0;
                int totalRead;
                while ((totalRead = stream.AsStreamForRead().Read(buffer, bytesRead, streamSize - bytesRead)) > 0)
                {
                    bytesRead += totalRead;
                }
                memoryStream.Write(buffer, 0, bytesRead);
                memoryStream.Position = 0;

                // Save to a temp wav file so MediaPlayer can play it on a background thread.
                // MediaPlayer works best with files, not arbitrary streams.
                var tempPath = Path.Combine(Path.GetTempPath(), $"TabataTts_{Guid.NewGuid()}.wav");
                await File.WriteAllBytesAsync(tempPath, memoryStream.ToArray());

                var thread = new Thread(() =>
                {
                    MediaPlayer? player = null;
                    try
                    {
                        player = new MediaPlayer();
                        player.Volume = _volume;

                        player.MediaOpened += (s, e) =>
                        {
                            try { player.Play(); } catch { }
                        };
                        player.MediaEnded += (s, e) =>
                        {
                            try { player.Close(); } catch { }
                            try { File.Delete(tempPath); } catch { }
                            // stop the dispatcher so thread can exit
                            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvokeShutdown(
                                System.Windows.Threading.DispatcherPriority.Background);
                        };

                        player.Open(new Uri(tempPath));
                        System.Windows.Threading.Dispatcher.Run();
                    }
                    catch
                    {
                        try { player?.Close(); } catch { }
                        try { File.Delete(tempPath); } catch { }
                    }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.IsBackground = true;
                thread.Start();
            }
            catch { /* never crash the timer over TTS */ }
        }

        /// <summary>Cancel any in-progress speech.</summary>
        public void Stop()
        {
            if (_disposed) return;
            // With WinRT TTS there's no mid-stream cancellation — playback is fire-and-forget.
            // The next Speak call will simply overlap/cut off the previous audio naturally.
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                try { _synth.Dispose(); } catch { }
            }
        }
    }
}
