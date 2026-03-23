using System.IO;
using System.Media;

namespace TabataTimer.Services
{
    /// <summary>
    /// Generates PCM WAV tones in memory and plays them via SoundPlayer.
    /// Three distinct sounds:
    ///   Warning beep  : short high tick (880 Hz, 80 ms)
    ///   Phase-end beep: double mid beep (660 Hz, 120 ms x2)
    ///   Final fanfare : ascending C5-E5-G5 (523/659/784 Hz)
    /// </summary>
    public class AudioService
    {
        private double _volume = 0.8;

        public void SetVolume(double volume) => _volume = Math.Clamp(volume, 0.0, 1.0);

        // ── Public API ──────────────────────────────────────────────────────

        public void PlayWarningBeep()  => PlayAsync(() => PlayTone(880, 80));

        public void PlayPhaseEndBeep() => PlayAsync(() =>
        {
            PlayTone(660, 120);
            Thread.Sleep(80);
            PlayTone(660, 120);
        });

        public void PlayFinalBeep() => PlayAsync(() =>
        {
            PlayTone(523, 200);   // C5
            Thread.Sleep(60);
            PlayTone(659, 200);   // E5
            Thread.Sleep(60);
            PlayTone(784, 450);   // G5 (held)
        });

        // ── Internals ───────────────────────────────────────────────────────

        private static void PlayAsync(Action action) => Task.Run(action);

        /// <summary>
        /// Synthesises a sine-wave tone and plays it synchronously on the
        /// calling (background) thread via SoundPlayer.
        /// </summary>
        private void PlayTone(double frequency, int durationMs)
        {
            if (_volume <= 0) return;

            try
            {
                const int sampleRate    = 44100;
                const int channels      = 1;
                const int bitsPerSample = 16;
                int sampleCount = (int)(sampleRate * durationMs / 1000.0);

                // Short linear fade-out over the last 10 % to kill end-click
                int fadeStart = (int)(sampleCount * 0.90);

                using var ms = new MemoryStream();
                using var bw = new BinaryWriter(ms);

                // RIFF / WAV header
                int dataSize   = sampleCount * channels * (bitsPerSample / 8);
                int byteRate   = sampleRate  * channels * (bitsPerSample / 8);
                int blockAlign = channels * (bitsPerSample / 8);

                bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                bw.Write(36 + dataSize);
                bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                bw.Write(16);
                bw.Write((short)1);                // PCM
                bw.Write((short)channels);
                bw.Write(sampleRate);
                bw.Write(byteRate);
                bw.Write((short)blockAlign);
                bw.Write((short)bitsPerSample);
                bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                bw.Write(dataSize);

                // PCM samples
                for (int i = 0; i < sampleCount; i++)
                {
                    double t        = (double)i / sampleRate;
                    double envelope = (i >= fadeStart)
                        ? 1.0 - (double)(i - fadeStart) / (sampleCount - fadeStart)
                        : 1.0;
                    double sample = Math.Sin(2 * Math.PI * frequency * t)
                                    * envelope * _volume;
                    bw.Write((short)(sample * short.MaxValue));
                }

                ms.Position = 0;

                using var player = new SoundPlayer(ms);
                player.PlaySync();
            }
            catch
            {
                // Never crash the UI over a failed beep
            }
        }
    }
}
