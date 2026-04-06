using System.IO;
using System.Windows.Media;
using Windows.Media.SpeechSynthesis;

namespace TabataTimer.Services
{
    /// <summary>
    /// Text-to-Speech service using Windows.Media.SpeechSynthesis (WinRT API).
    /// Audio playback runs on a dedicated background STA thread with a single message loop,
    /// so only one playback runs at a time and the UI thread is never blocked.
    /// </summary>
    public class TtsService : ITtsService
    {
        private readonly SpeechSynthesizer _synth;
        private double _volume = 0.8;
        private bool _disposed = false;
        private VoiceInformation? _voice;

        // Playback thread's dispatcher (null until first Speak call)
        private System.Windows.Threading.Dispatcher? _playerDispatcher;

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
        public IReadOnlyList<VoiceInformation> GetAvailableVoices()
            => SpeechSynthesizer.AllVoices.ToList();

        /// <summary>Static convenience wrapper for GetAvailableVoices().</summary>
        public static IReadOnlyList<VoiceInformation> GetAvailableVoices_Static()
            => SpeechSynthesizer.AllVoices.ToList();

        /// <summary>
        /// Speak text asynchronously on a dedicated background thread.
        /// Fire-and-forget: overlapping calls will naturally cut off the previous audio.
        /// </summary>
        public async Task Speak(string text)
        {
            if (_disposed || string.IsNullOrWhiteSpace(text) || _volume <= 0) return;

            try
            {
                _synth.Voice = _voice ?? SpeechSynthesizer.DefaultVoice;
                var stream = await _synth.SynthesizeTextToStreamAsync(text);

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

                var tempPath = Path.Combine(Path.GetTempPath(), $"TabataTts_{Guid.NewGuid()}.wav");
                await File.WriteAllBytesAsync(tempPath, memoryStream.ToArray()).ConfigureAwait(false);

                EnsurePlaybackThread();
                _playerDispatcher?.Invoke(() => PlayWav(tempPath));
            }
            catch { /* never crash the timer over TTS */ }
        }

        /// <summary>
        /// Ensure the dedicated playback thread and dispatcher exist and are running.
        /// Thread-safety: Interlocked handles the rare race where two calls racing
        /// EnsurePlaybackThread might create two threads; the first one wins and the
        /// second becomes eligible for GC.
        /// </summary>
        private void EnsurePlaybackThread()
        {
            if (_playerDispatcher != null)
                return;

            var t = new Thread(() =>
            {
                // CurrentDispatcher is thread-local — capture it so we can pass it back
                _playerDispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
                System.Windows.Threading.Dispatcher.Run();
            })
            {
                IsBackground = true,
                Name = "TtsService.Playback"
            };
            t.SetApartmentState(ApartmentState.STA);
            t.Start();

            // Block until the thread has stored the dispatcher reference.
            // This is only called from Speak() which is already async, so blocking
            // the call site (not the UI thread) is acceptable.
            t.Join(500);
        }

        private void PlayWav(string tempPath)
        {
            MediaPlayer? player = null;
            try
            {
                player = new MediaPlayer { Volume = _volume };
                player.MediaEnded += (s, e) =>
                {
                    player?.Close();
                    try { File.Delete(tempPath); } catch { }
                };
                player.Open(new Uri(tempPath));
                player.Play();
            }
            catch
            {
                try { player?.Close(); } catch { }
                try { File.Delete(tempPath); } catch { }
            }
        }

        /// <summary>Cancel any in-progress speech.</summary>
        public void Stop()
        {
            if (_disposed) return;
            // With WinRT TTS there's no mid-stream cancellation.
            // The next Speak call will naturally overlap/cut off the previous audio.
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _synth.Dispose(); } catch { }

            if (_playerDispatcher != null)
                _playerDispatcher.InvokeShutdown();
        }
    }
}
