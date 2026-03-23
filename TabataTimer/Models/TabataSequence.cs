using Newtonsoft.Json;

namespace TabataTimer.Models
{
    public class TabataSequence
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public int WaitSeconds { get; set; } = 10;
        public int Repeats { get; set; } = 8;
        public int WorkSeconds { get; set; } = 20;
        public int RestSeconds { get; set; } = 10;

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

    public class AppSettings
    {
        public List<TabataSequence> Sequences { get; set; } = new();
        public double Volume { get; set; } = 0.8;
        public bool WarningBeepEnabled { get; set; } = true;
    }
}
