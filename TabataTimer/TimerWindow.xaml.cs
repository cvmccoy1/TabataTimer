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
        // ── Dependencies ────────────────────────────────────────────────────
        private readonly TabataSequence _sequence;
        private readonly AppSettings    _settings;
        private readonly AudioService   _audio = new();
        private readonly TtsService     _tts   = new();
        private readonly CallOutEngine  _callOut;

        // ── Timer state ─────────────────────────────────────────────────────
        private DispatcherTimer? _timer;
        private TimerPhase _phase            = TimerPhase.Idle;
        private bool       _isPaused         = false;
        private int        _phaseSecondsLeft;
        private int        _totalSecondsElapsed;
        private int        _currentRound;

        /// <summary>
        /// Counts down the 2-second delay before the next call-out.
        /// Fires on the main tick; when _callOutDelaySeconds reaches 0,
        /// SpeakExercise is called and the counter is reset to -1 (inactive).
        /// </summary>
        private int _callOutDelaySeconds = -1;

        // Phase colors
        private static readonly Color WaitColor = (Color)ColorConverter.ConvertFromString("#A0A0A0");
        private static readonly Color WorkColor = (Color)ColorConverter.ConvertFromString("#22C55E");
        private static readonly Color RestColor = (Color)ColorConverter.ConvertFromString("#FF4500");
        private static readonly Color DoneColor = (Color)ColorConverter.ConvertFromString("#EAB308");

        public event EventHandler? SettingsChanged;

        public TimerWindow(TabataSequence sequence, AppSettings settings)
        {
            _sequence = sequence;
            _settings = settings;
            _callOut  = new CallOutEngine(_sequence);

            InitializeComponent();

            SequenceNameText.Text      = _sequence.Name.ToUpperInvariant();
            VolumeSlider.Value         = _settings.Volume;
            WarningBeepCheck.IsChecked = _settings.WarningBeepEnabled;
            _audio.SetVolume(_settings.Volume);
            _tts.SetVolume(_settings.Volume);
            _tts.SetVoice(_sequence.VoiceName);

            ResetDisplay();

            Loaded += (s, e) =>
            {
                var layout = _settings.TimerWindowLayout;
                if (!double.IsNaN(layout.Left) && !double.IsNaN(layout.Top))
                {
                    Left = layout.Left;
                    Top = layout.Top;
                }
                //The width and height are currently fixed to avoid layout issues with the call-out text.
                //if (!double.IsNaN(layout.Width) && layout.Width > 0)
                //    Width = Math.Max(layout.Width, MinWidth);
                //if (!double.IsNaN(layout.Height) && layout.Height > 0)
                //    Height = Math.Max(layout.Height, MinHeight);
            };
            Closing += (s, e) =>
            {
                _settings.Volume = VolumeSlider.Value;
                _settings.WarningBeepEnabled = WarningBeepCheck.IsChecked == true;
                _settings.TimerWindowLayout = new WindowLayout
                {
                    Left = Left,
                    Top = Top,
                    Width = 0.0,
                    Height = 0.0
                };
            };
        }

        // ── UI Reset ─────────────────────────────────────────────────────────
        private void ResetDisplay()
        {
            _phase                    = TimerPhase.Idle;
            _isPaused                 = false;
            _currentRound             = 0;
            _totalSecondsElapsed      = 0;
            _phaseSecondsLeft         = _sequence.RestSeconds;
            _callOutDelaySeconds      = -1;

            UpdatePhaseUI(TimerPhase.Idle);
            CountdownDisplay.Text = FormatTime(_sequence.RestSeconds);
            TotalDisplay.Text     = "00:00";
            RoundDisplay.Text     = $"0 of {_sequence.Repeats}";

            StartButton.IsEnabled = true;
            PauseButton.IsEnabled = false;
            StopButton.IsEnabled  = false;
            PauseButton.Content   = "⏸  PAUSE";
        }

        // ── Controls ─────────────────────────────────────────────────────────
        private void Start_Click(object sender, RoutedEventArgs e)
        {
            _isPaused                 = false;
            _currentRound             = 0;
            _totalSecondsElapsed      = 0;
            _callOut.Reset();

            StartButton.IsEnabled = false;
            PauseButton.IsEnabled = true;
            StopButton.IsEnabled  = true;

            if (_sequence.WaitSeconds > 0)
            {
                // Schedule the first call-out for 2 seconds from now (during Wait).
                // EnterPhase(Work) for round 1 must NOT trigger its own call-out.
                _callOutDelaySeconds = 2;
                EnterPhase(TimerPhase.Wait);
            }
            else
            {
                // No Wait — schedule the first call-out for 2 seconds from now,
                // then go straight to Work without a call-out yet.
                _callOutDelaySeconds = 2;
                EnterPhase(TimerPhase.Work);
            }

            StartTimer();
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            if (_isPaused)
            {
                _isPaused           = false;
                PauseButton.Content = "⏸  PAUSE";
                _timer?.Start();
            }
            else
            {
                _isPaused           = true;
                PauseButton.Content = "▶  RESUME";
                _timer?.Stop();
                _tts.Stop();
            }
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            StopTimer();
            _tts.Stop();
            ResetDisplay();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            StopTimer();
            _tts.Stop();
            Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            StopTimer();
            _tts.Stop();
            _tts.Dispose();
            _audio.Dispose();
        }

        // ── Timer core ───────────────────────────────────────────────────────
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

            // Count down the call-out delay and speak when it expires
            if (_callOutDelaySeconds > 0)
            {
                _callOutDelaySeconds--;
            }
            else if (_callOutDelaySeconds == 0)
            {
                _callOutDelaySeconds = -1;
                SpeakExercise();
            }

            // Decrement first so the display briefly shows "00" before AdvancePhase
            // fires the phase-end beep on the next tick (avoiding overlap).
            _phaseSecondsLeft--;

            // Warning beeps at 3, 2, 1 seconds remaining — now checked after decrement
            // so beeps fire at display values 3→2, 2→1, 1→0 as intended
            if (_settings.WarningBeepEnabled
                && _phaseSecondsLeft >= 1
                && _phaseSecondsLeft <= 3
                && _phase != TimerPhase.Done)
            {
                _audio.PlayWarningBeep();
            }

            if (_phaseSecondsLeft <= 0)
                AdvancePhase();
            else
                CountdownDisplay.Text = FormatTime(_phaseSecondsLeft);
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
                    // Schedule the call-out for 2 seconds from now (during Rest)
                    _callOutDelaySeconds = 2;
                    break;
            }

            CountdownDisplay.Text = FormatTime(_phaseSecondsLeft);
        }

        private void AdvancePhase()
        {
            switch (_phase)
            {
                case TimerPhase.Wait:
                    _audio.PlayPhaseEndBeep();
                    EnterPhase(TimerPhase.Work);
                    break;

                case TimerPhase.Work:
                    if (_currentRound >= _sequence.Repeats)
                    {
                        _audio.PlayFinalBeep();
                        _tts.Speak("Workout complete. Great job!");
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

            PhaseLabel.Text             = "DONE!";
            PhaseLabel.Foreground       = new SolidColorBrush(DoneColor);
            CountdownDisplay.Text       = "00:00";
            CountdownDisplay.Foreground = new SolidColorBrush(DoneColor);
            RoundDisplay.Text           = $"{_sequence.Repeats} of {_sequence.Repeats}";
            ExerciseBorder.Visibility    = Visibility.Collapsed;

            StartButton.IsEnabled = true;
            PauseButton.IsEnabled = false;
            StopButton.IsEnabled  = false;
        }

        // ── TTS call-out ─────────────────────────────────────────────────────

        /// <summary>Ask the engine for the next exercise and speak it if non-empty.</summary>
        private void SpeakExercise()
        {
            if (_sequence.CallOutMode == CallOutMode.Off) return;
            var exercise = _callOut.Next();
            if (!string.IsNullOrWhiteSpace(exercise))
            {
                ExerciseLabel.Text = exercise.ToUpperInvariant();
                ExerciseBorder.Visibility = Visibility.Visible;
                _tts.Speak(exercise);
            }
        }

        // ── Phase UI ─────────────────────────────────────────────────────────
        private void UpdatePhaseUI(TimerPhase phase)
        {
            switch (phase)
            {
                case TimerPhase.Idle:
                    PhaseLabel.Text             = "READY";
                    PhaseLabel.Foreground       = new SolidColorBrush(WaitColor);
                    CountdownDisplay.Foreground = new SolidColorBrush(Colors.WhiteSmoke);
                    ExerciseBorder.Visibility   = Visibility.Collapsed;
                    break;
                case TimerPhase.Wait:
                    PhaseLabel.Text             = "WAIT";
                    PhaseLabel.Foreground       = new SolidColorBrush(WaitColor);
                    CountdownDisplay.Foreground = new SolidColorBrush(WaitColor);
                    ExerciseBorder.Visibility   = Visibility.Collapsed;
                    break;
                case TimerPhase.Work:
                    PhaseLabel.Text             = "WORK";
                    PhaseLabel.Foreground       = new SolidColorBrush(WorkColor);
                    CountdownDisplay.Foreground = new SolidColorBrush(WorkColor);
                    // Border is shown by SpeakExercise when TTS fires
                    break;
                case TimerPhase.Rest:
                    PhaseLabel.Text             = "REST";
                    PhaseLabel.Foreground       = new SolidColorBrush(RestColor);
                    CountdownDisplay.Foreground = new SolidColorBrush(RestColor);
                    ExerciseBorder.Visibility   = Visibility.Collapsed;
                    break;
            }
        }

        // ── Settings controls ────────────────────────────────────────────────
        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_audio == null) return;
            _audio.SetVolume(VolumeSlider.Value);
            _tts.SetVolume(VolumeSlider.Value);
            _settings.Volume = VolumeSlider.Value;
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void WarningBeep_Changed(object sender, RoutedEventArgs e)
        {
            if (_settings == null) return;
            _settings.WarningBeepEnabled = WarningBeepCheck.IsChecked == true;
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private static string FormatTime(int totalSeconds)
        {
            if (totalSeconds < 0) totalSeconds = 0;
            int m = totalSeconds / 60;
            int s = totalSeconds % 60;
            return $"{m:D2}:{s:D2}";
        }
    }
}
