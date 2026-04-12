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
        public List<string> CallOutList { get; set; } = [];

        /// <summary>
        /// The display name of the TTS voice to use for this sequence.
        /// Null uses the system default voice.
        /// </summary>
        public string? VoiceName { get; set; }

        /// <summary>
        /// Volume for this sequence (0.0 – 1.0). NaN means use the app default.
        /// </summary>
        public double Volume { get; set; } = double.NaN;

        /// <summary>
        /// Whether the 3-second warning beep is enabled for this sequence.
        /// </summary>
        public bool WarningBeepEnabled { get; set; } = true;

        public string WaitDisplay => FormatTime(WaitSeconds);
        public string WorkDisplay => FormatTime(WorkSeconds);
        public string RestDisplay => FormatTime(RestSeconds);

        public string TotalDisplay
        {
            get
            {
                int total = WaitSeconds + (Repeats * WorkSeconds) + ((Repeats - 1) * RestSeconds);
                return FormatTotalTime(total);
            }
        }

        private static string FormatTime(int seconds)
        {
            int m = seconds / 60;
            int s = seconds % 60;
            return $"{m:D2}:{s:D2}";
        }

        private static string FormatTotalTime(int seconds)
        {
            int h = seconds / 3600;
            int m = (seconds % 3600) / 60;
            int s = seconds % 60;
            return h > 0 ? $"{h}:{m:D2}:{s:D2}" : $"{m:D2}:{s:D2}";
        }
    }

    public class WindowLayout
    {
        public double Left { get; set; } = double.NaN;
        public double Top { get; set; } = double.NaN;
        public double Width { get; set; } = 900;
        public double Height { get; set; } = 650;
    }

    public class SequenceFolder
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "New Folder";
        public List<SequenceFolder> SubFolders { get; set; } = [];
        public List<TabataSequence> Sequences { get; set; } = [];
    }

    public class AppSettings
    {
        public List<TabataSequence> Sequences { get; set; } = [];
        public List<SequenceFolder> RootFolders { get; set; } = [];
        public double Volume { get; set; } = 0.8;
        public bool WarningBeepEnabled { get; set; } = true;
        public WindowLayout MainWindowLayout { get; set; } = new();
        public Dictionary<Guid, WindowLayout> EditDialogLayouts { get; set; } = [];
        public WindowLayout TimerWindowLayout { get; set; } = new();
    }
}
