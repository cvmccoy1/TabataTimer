namespace TabataTimer
{
    /// <summary>
    /// Hard limits for all sequence fields.
    /// Used by code-behind (via TimerConstraints) and XAML (via TC).
    /// </summary>
    public static class TimerConstraints
    {
        public static int WaitMin    => 0;
        public static int WaitMax    => 30;

        public static int RepeatsMin => 1;
        public static int RepeatsMax => 25;

        public static int WorkMin    => 1;
        public static int WorkMax    => 60;

        public static int RestMin    => 1;
        public static int RestMax    => 30;
    }
}
