using Newtonsoft.Json;

namespace TabataTimer.Models
{
    public enum CallOutMode { Follow, Repeat, Random, Off }

    public class TabataSequence
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public int WaitSeconds { get; set; } = 10;
        public int Repeats { get; set; } = 8;
        public int WorkSeconds { get; set; } = 20;
        public int RestSeconds { get; set; } = 10;

        public CallOutMode CallOutMode { get; set; } = CallOutMode.Off;

        /// <summary>
        /// Each entry corresponds to one slot in the call-out list.
        /// A slot may be empty, a single exercise, or comma-separated exercises.
        /// </summary>
        public List<string> CallOutList { get; set; } = new();

        /// <summary>
        /// The display name of the TTS voice to use for this sequence.
        /// Null uses the system default voice.
        /// </summary>
        public string? VoiceName { get; set; }

        public string WaitDisplay => FormatTime(WaitSeconds);
        public string WorkDisplay => FormatTime(WorkSeconds);
        public string RestDisplay => FormatTime(RestSeconds);

        private static string FormatTime(int seconds)
        {
            int m = seconds / 60;
            int s = seconds % 60;
            return $"{m:D2}:{s:D2}";
        }
    }

    public class WindowLayout
    {
        public double Left { get; set; } = double.NaN;
        public double Top { get; set; } = double.NaN;
        public double Width { get; set; } = 900;
        public double Height { get; set; } = 650;
    }

    public class AppSettings
    {
        public List<TabataSequence> Sequences { get; set; } = new();
        public double Volume { get; set; } = 0.8;
        public bool WarningBeepEnabled { get; set; } = true;
        public WindowLayout MainWindowLayout { get; set; } = new();
        public Dictionary<Guid, WindowLayout> EditDialogLayouts { get; set; } = new();
        public WindowLayout TimerWindowLayout { get; set; } = new();
    }
}
