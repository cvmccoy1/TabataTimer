using System.Media;
using System.Runtime.InteropServices;

namespace TabataTimer.Services
{
    /// <summary>
    /// Generates three distinct beep types using Windows Beep API and synthesized WAV data.
    /// - Warning beep: short, mid-pitch tick (countdown warning)
    /// - Sequence-end beep: double beep (end of work or rest phase)
    /// - Final beep: ascending 3-tone fanfare (workout complete)
    /// </summary>
    public class AudioService
    {
        private double _volume = 0.8;

        public void SetVolume(double volume) => _volume = Math.Clamp(volume, 0, 1);

        // Short single tick — warning countdown (3, 2, 1)
        public void PlayWarningBeep()
        {
            if (_volume <= 0) return;
            Task.Run(() =>
            {
                try { Console.Beep(880, 80); } catch { }
            });
        }

        // Double beep — end of a phase (end of Work, end of Rest, end of Wait)
        public void PlayPhaseEndBeep()
        {
            if (_volume <= 0) return;
            Task.Run(() =>
            {
                try
                {
                    Console.Beep(660, 120);
                    Thread.Sleep(60);
                    Console.Beep(660, 120);
                }
                catch { }
            });
        }

        // Ascending 3-tone fanfare — workout fully complete
        public void PlayFinalBeep()
        {
            if (_volume <= 0) return;
            Task.Run(() =>
            {
                try
                {
                    Console.Beep(523, 180);  // C5
                    Thread.Sleep(60);
                    Console.Beep(659, 180);  // E5
                    Thread.Sleep(60);
                    Console.Beep(784, 400);  // G5 (held)
                }
                catch { }
            });
        }
    }
}
