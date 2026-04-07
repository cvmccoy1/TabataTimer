using System.IO;
using System.Windows.Media;
using System.Windows.Threading;

namespace TabataTimer.Services
{
    /// <summary>
    /// Plays synthesized tones via WPF MediaPlayer, which correctly routes
    /// to the Windows default audio device (including HDMI/Bluetooth outputs).
    /// Tones are written as temporary WAV files and played through MediaPlayer.
    /// </summary>
    public class AudioService : IAudioService
    {
        private double _volume = 0.8;
        private readonly string _tempDir;

        // Pre-generate the sound files once at startup
        private readonly string _warningFile;
        private readonly string _phaseEndFile;
        private readonly string _finalFile;
        private readonly string _midWorkFile;

        public AudioService()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "TabataTimer");
            Directory.CreateDirectory(_tempDir);

            // Clean up any orphaned .wav files left behind from a previous run
            // (they're unlocked by now since the app was closed).
            try
            {
                foreach (var f in Directory.GetFiles(_tempDir, "*.wav"))
                    File.Delete(f);
            }
            catch { /* best-effort */ }

            // Use a unique suffix per instance so multiple TimerWindows don't
            // collide on the same temp files (MediaPlayer may still hold a lock
            // on a file from a previous AudioService instance).
            string id = Guid.NewGuid().ToString("N");
            _warningFile  = Path.Combine(_tempDir, $"warning_{id}.wav");
            _phaseEndFile = Path.Combine(_tempDir, $"phaseend_{id}.wav");
            _finalFile    = Path.Combine(_tempDir, $"final_{id}.wav");
            _midWorkFile  = Path.Combine(_tempDir, $"midwork_{id}.wav");

            // Warning: short high tick
            WriteWav(_warningFile, new[] { (880.0, 90) });

            // Phase end: double mid beep
            WriteWav(_phaseEndFile, new[] { (660.0, 130), (0.0, 80), (660.0, 130) });

            // Final: ascending C5-E5-G5 fanfare
            WriteWav(_finalFile, new[] { (523.0, 210), (0.0, 60), (659.0, 210), (0.0, 60), (784.0, 480) });

            // Mid-work: ascending two-note signal for * exercises
            WriteWav(_midWorkFile, new[] { (523.0, 120), (0.0, 40), (660.0, 160) });
        }

        public void SetVolume(double volume) => _volume = Math.Clamp(volume, 0.0, 1.0);

        public void PlayWarningBeep()  => PlayFile(_warningFile);
        public void PlayPhaseEndBeep() => PlayFile(_phaseEndFile);
        public void PlayFinalBeep()    => PlayFile(_finalFile);
        public void PlayMidWorkBeep()  => PlayFile(_midWorkFile);

        public void Dispose()
        {
            TryDelete(_warningFile);
            TryDelete(_phaseEndFile);
            TryDelete(_finalFile);
            TryDelete(_midWorkFile);
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        // ── Playback ────────────────────────────────────────────────────────

        private void PlayFile(string path)
        {
            if (_volume <= 0 || !File.Exists(path)) return;

            // MediaPlayer must be created and used on an STA thread with a
            // Dispatcher. Spin up a dedicated STA thread for each play call.
            var thread = new Thread(() =>
            {
                try
                {
                    var player = new MediaPlayer();
                    player.Volume = _volume;
                    player.Open(new Uri(path, UriKind.Absolute));

                    // Wait for it to open then play
                    player.MediaOpened += (s, e) => player.Play();

                    // Run a Dispatcher loop so MediaPlayer events fire
                    // Exit after a generous timeout (longest sound ~1.1 s)
                    var dispatcher = Dispatcher.CurrentDispatcher;
                    dispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Background);

                    // Give it time to actually play before shutting dispatcher
                    // We push a delayed shutdown instead
                    var timer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher)
                    {
                        Interval = TimeSpan.FromMilliseconds(1500)
                    };
                    timer.Tick += (s, e) =>
                    {
                        timer.Stop();
                        player.Stop();
                        player.Close();
                        dispatcher.InvokeShutdown();
                    };
                    timer.Start();

                    // Restart open in case MediaOpened already fired
                    player.Play();

                    Dispatcher.Run();
                }
                catch { /* never crash UI over audio */ }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
        }

        // ── WAV synthesis ───────────────────────────────────────────────────

        /// <summary>
        /// Writes a multi-segment WAV. Each tuple is (frequency Hz, duration ms).
        /// Use frequency = 0 for silence gaps.
        /// </summary>
        private static void WriteWav(string path, IEnumerable<(double freq, int ms)> segments)
        {
            const int sampleRate    = 44100;
            const int channels      = 1;
            const int bitsPerSample = 16;

            var allSamples = new List<short>();

            foreach (var (freq, ms) in segments)
            {
                int count     = (int)(sampleRate * ms / 1000.0);
                int fadeStart = (int)(count * 0.88);

                for (int i = 0; i < count; i++)
                {
                    double envelope = (i >= fadeStart && freq > 0)
                        ? 1.0 - (double)(i - fadeStart) / (count - fadeStart)
                        : 1.0;
                    double sample = (freq > 0)
                        ? Math.Sin(2 * Math.PI * freq * i / sampleRate) * envelope * 0.8
                        : 0.0;
                    allSamples.Add((short)(sample * short.MaxValue));
                }
            }

            int dataSize   = allSamples.Count * (bitsPerSample / 8);
            int byteRate   = sampleRate * channels * (bitsPerSample / 8);
            int blockAlign = channels * (bitsPerSample / 8);

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);

            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + dataSize);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);
            bw.Write((short)1);
            bw.Write((short)channels);
            bw.Write(sampleRate);
            bw.Write(byteRate);
            bw.Write((short)blockAlign);
            bw.Write((short)bitsPerSample);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            bw.Write(dataSize);

            foreach (var s in allSamples)
                bw.Write(s);
        }
    }
}
