using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using TabataTimer.Models;
using TabataTimer.Services;

namespace TabataTimer
{
    public enum TimerPhase { Idle, Wait, Work, Rest, Done }

    public partial class TimerWindow : Window
    {
        // ---- Dependencies ----
        private readonly TabataSequence _sequence;
        private readonly AppSettings _settings;
        private readonly AudioService _audio = new();

        // ---- State ----
        private DispatcherTimer? _timer;
        private TimerPhase _phase = TimerPhase.Idle;
        private bool _isPaused = false;

        private int _phaseSecondsLeft;   // countdown for current phase
        private int _totalSecondsElapsed; // count-up total
        private int _currentRound;        // 0-based, becomes 1 when first Work begins

        // Colors for phase
        private static readonly Color WaitColor  = (Color)ColorConverter.ConvertFromString("#A0A0A0");
        private static readonly Color WorkColor  = (Color)ColorConverter.ConvertFromString("#22C55E");
        private static readonly Color RestColor  = (Color)ColorConverter.ConvertFromString("#FF4500");
        private static readonly Color DoneColor  = (Color)ColorConverter.ConvertFromString("#EAB308");

        public event EventHandler? SettingsChanged;

        public TimerWindow(TabataSequence sequence, AppSettings settings)
        {
            InitializeComponent();
            _sequence = sequence;
            _settings = settings;

            SequenceNameText.Text = sequence.Name.ToUpperInvariant();
            VolumeSlider.Value = settings.Volume;
            WarningBeepCheck.IsChecked = settings.WarningBeepEnabled;
            _audio.SetVolume(settings.Volume);

            ResetDisplay();
        }

        // ----------------------------------------------------------------
        // UI Reset
        // ----------------------------------------------------------------
        private void ResetDisplay()
        {
            _phase = TimerPhase.Idle;
            _isPaused = false;
            _currentRound = 0;
            _totalSecondsElapsed = 0;
            _phaseSecondsLeft = _sequence.RestSeconds; // initially show Rest time

            UpdatePhaseUI(TimerPhase.Idle);
            CountdownDisplay.Text = FormatTime(_sequence.RestSeconds);
            TotalDisplay.Text = "00:00";
            RoundDisplay.Text = $"0 of {_sequence.Repeats}";

            StartButton.IsEnabled = true;
            PauseButton.IsEnabled = false;
            StopButton.IsEnabled = false;
            PauseButton.Content = "⏸  PAUSE";
        }

        // ----------------------------------------------------------------
        // Timer Controls
        // ----------------------------------------------------------------
        private void Start_Click(object sender, RoutedEventArgs e)
        {
            _isPaused = false;
            _currentRound = 0;
            _totalSecondsElapsed = 0;

            StartButton.IsEnabled = false;
            PauseButton.IsEnabled = true;
            StopButton.IsEnabled = true;

            if (_sequence.WaitSeconds > 0)
                EnterPhase(TimerPhase.Wait);
            else
                EnterPhase(TimerPhase.Work);

            StartTimer();
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            if (_isPaused)
            {
                _isPaused = false;
                PauseButton.Content = "⏸  PAUSE";
                _timer?.Start();
            }
            else
            {
                _isPaused = true;
                PauseButton.Content = "▶  RESUME";
                _timer?.Stop();
            }
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            StopTimer();
            ResetDisplay();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            StopTimer();
            Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            StopTimer();
        }

        // ----------------------------------------------------------------
        // Timer Core
        // ----------------------------------------------------------------
        private void StartTimer()
        {
            _timer?.Stop();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void StopTimer()
        {
            _timer?.Stop();
            _timer = null;
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _totalSecondsElapsed++;
            TotalDisplay.Text = FormatTime(_totalSecondsElapsed);

            // Warning beeps: beep at 3, 2, 1 seconds remaining
            if (_settings.WarningBeepEnabled && _phaseSecondsLeft <= 3 && _phaseSecondsLeft > 0 && _phase != TimerPhase.Done)
            {
                _audio.PlayWarningBeep();
            }

            _phaseSecondsLeft--;

            if (_phaseSecondsLeft <= 0)
            {
                AdvancePhase();
            }
            else
            {
                CountdownDisplay.Text = FormatTime(_phaseSecondsLeft);
            }
        }

        private void EnterPhase(TimerPhase phase)
        {
            _phase = phase;
            UpdatePhaseUI(phase);

            switch (phase)
            {
                case TimerPhase.Wait:
                    _phaseSecondsLeft = _sequence.WaitSeconds;
                    break;
                case TimerPhase.Work:
                    _currentRound++;
                    _phaseSecondsLeft = _sequence.WorkSeconds;
                    RoundDisplay.Text = $"{_currentRound} of {_sequence.Repeats}";
                    break;
                case TimerPhase.Rest:
                    _phaseSecondsLeft = _sequence.RestSeconds;
                    break;
            }

            CountdownDisplay.Text = FormatTime(_phaseSecondsLeft);
        }

        private void AdvancePhase()
        {
            // Play end-of-phase beep
            switch (_phase)
            {
                case TimerPhase.Wait:
                    _audio.PlayPhaseEndBeep();
                    EnterPhase(TimerPhase.Work);
                    break;

                case TimerPhase.Work:
                    // Last round's work phase transitions to Done
                    if (_currentRound >= _sequence.Repeats)
                    {
                        _audio.PlayFinalBeep();
                        EnterDone();
                    }
                    else
                    {
                        _audio.PlayPhaseEndBeep();
                        EnterPhase(TimerPhase.Rest);
                    }
                    break;

                case TimerPhase.Rest:
                    _audio.PlayPhaseEndBeep();
                    EnterPhase(TimerPhase.Work);
                    break;
            }
        }

        private void EnterDone()
        {
            _phase = TimerPhase.Done;
            StopTimer();

            PhaseLabel.Text = "DONE!";
            PhaseLabel.Foreground = new SolidColorBrush(DoneColor);
            CountdownDisplay.Text = "00:00";
            CountdownDisplay.Foreground = new SolidColorBrush(DoneColor);
            RoundDisplay.Text = $"{_sequence.Repeats} of {_sequence.Repeats}";

            StartButton.IsEnabled = true;
            PauseButton.IsEnabled = false;
            StopButton.IsEnabled = false;
        }

        // ----------------------------------------------------------------
        // Phase UI Update
        // ----------------------------------------------------------------
        private void UpdatePhaseUI(TimerPhase phase)
        {
            switch (phase)
            {
                case TimerPhase.Idle:
                    PhaseLabel.Text = "READY";
                    PhaseLabel.Foreground = new SolidColorBrush(WaitColor);
                    CountdownDisplay.Foreground = new SolidColorBrush(Colors.WhiteSmoke);
                    break;
                case TimerPhase.Wait:
                    PhaseLabel.Text = "WAIT";
                    PhaseLabel.Foreground = new SolidColorBrush(WaitColor);
                    CountdownDisplay.Foreground = new SolidColorBrush(WaitColor);
                    break;
                case TimerPhase.Work:
                    PhaseLabel.Text = "WORK";
                    PhaseLabel.Foreground = new SolidColorBrush(WorkColor);
                    CountdownDisplay.Foreground = new SolidColorBrush(WorkColor);
                    break;
                case TimerPhase.Rest:
                    PhaseLabel.Text = "REST";
                    PhaseLabel.Foreground = new SolidColorBrush(RestColor);
                    CountdownDisplay.Foreground = new SolidColorBrush(RestColor);
                    break;
            }
        }

        // ----------------------------------------------------------------
        // Settings Controls
        // ----------------------------------------------------------------
        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _audio.SetVolume(VolumeSlider.Value);
            _settings.Volume = VolumeSlider.Value;
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void WarningBeep_Changed(object sender, RoutedEventArgs e)
        {
            _settings.WarningBeepEnabled = WarningBeepCheck.IsChecked == true;
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------
        private static string FormatTime(int totalSeconds)
        {
            if (totalSeconds < 0) totalSeconds = 0;
            int m = totalSeconds / 60;
            int s = totalSeconds % 60;
            return $"{m:D2}:{s:D2}";
        }
    }
}
