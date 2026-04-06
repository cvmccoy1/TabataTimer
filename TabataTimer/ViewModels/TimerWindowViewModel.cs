using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabataTimer.Models;
using TabataTimer.Services;

namespace TabataTimer.ViewModels;

public enum TimerPhase { Idle, Wait, Work, Rest, Done }

public partial class TimerWindowViewModel : ViewModelBase, IDisposable
{
    // ── Injected services ────────────────────────────────────────────────────
    private readonly TabataSequence _sequence;
    private readonly AppSettings _settings;
    private readonly IAudioService _audio;
    private readonly ITtsService _tts;
    private readonly ICallOutEngine _callOut;

    // ── Timer ───────────────────────────────────────────────────────────────
    private DispatcherTimer? _timer;
    private int _callOutDelaySeconds = -1;

    // ── Observable state ────────────────────────────────────────────────────
    [ObservableProperty]
    private TimerPhase _phase = TimerPhase.Idle;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private int _phaseSecondsLeft;

    [ObservableProperty]
    private int _totalSecondsElapsed;

    [ObservableProperty]
    private int _currentRound;

    [ObservableProperty]
    private string _countdownDisplay = "00:00";

    [ObservableProperty]
    private string _totalDisplay = "00:00";

    [ObservableProperty]
    private string _roundDisplay = "0 of 0";

    [ObservableProperty]
    private string _phaseLabel = "READY";

    [ObservableProperty]
    private string _exerciseLabel = "";

    public string SequenceName => _sequence.Name;

    [ObservableProperty]
    private bool _exerciseVisible;

    [ObservableProperty]
    private SolidColorBrush _phaseLabelForeground = new(Colors.Gray);

    [ObservableProperty]
    private SolidColorBrush _countdownForeground = new(Colors.WhiteSmoke);

    [ObservableProperty]
    private bool _startEnabled = true;

    [ObservableProperty]
    private bool _pauseEnabled;

    [ObservableProperty]
    private bool _stopEnabled;

    [ObservableProperty]
    private string _pauseButtonContent = "⏸  PAUSE";

    [ObservableProperty]
    private double _volume;

    [ObservableProperty]
    private bool _warningBeepEnabled;

    // ── Color constants ─────────────────────────────────────────────────────
    private static readonly SolidColorBrush WaitColorBrush = new(Color.FromRgb(0xA0, 0xA0, 0xA0));
    private static readonly SolidColorBrush WorkColorBrush = new(Color.FromRgb(0x22, 0xC5, 0x5E));
    private static readonly SolidColorBrush RestColorBrush = new(Color.FromRgb(0xFF, 0x45, 0x00));
    private static readonly SolidColorBrush DoneColorBrush = new(Color.FromRgb(0xEA, 0xB3, 0x08));

    // ── Commands ────────────────────────────────────────────────────────────
    [RelayCommand]
    private void Start() => DoStart();

    [RelayCommand]
    private void Pause() => DoPause();

    [RelayCommand]
    private void Stop() => DoStop();

    [RelayCommand]
    private void Exit()
    {
        ExitRequested?.Invoke(this, EventArgs.Empty);
        Dispose();
    }

    // ── Settings change notification ─────────────────────────────────────────
    public event EventHandler? SettingsChanged;

    public event EventHandler? ExitRequested;

    // ── Window layout ────────────────────────────────────────────────────────
    public WindowLayout CurrentLayout => _settings.TimerWindowLayout;

    // ── Constructor ───────────────────────────────────────────────────────
    public TimerWindowViewModel(
        TabataSequence sequence,
        AppSettings settings,
        IAudioService audio,
        ITtsService tts,
        ICallOutEngine callOut)
    {
        _sequence = sequence;
        _settings = settings;
        _audio = audio;
        _tts = tts;
        _callOut = callOut;

        Volume = double.IsNaN(_sequence.Volume) ? settings.Volume : _sequence.Volume;
        WarningBeepEnabled = _sequence.WarningBeepEnabled;

        _audio.SetVolume(Volume);
        _tts.SetVolume(Volume);
        _tts.SetVoice(sequence.VoiceName);

        ResetDisplay();
    }

    // ── Partial method overrides for volume/warning beep ───────────────────────
    partial void OnVolumeChanged(double value)
    {
        _audio.SetVolume(value);
        _tts.SetVolume(value);
        _sequence.Volume = value;
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnWarningBeepEnabledChanged(bool value)
    {
        _sequence.WarningBeepEnabled = value;
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Timer lifecycle ──────────────────────────────────────────────────────
    private void DoStart()
    {
        IsPaused = false;
        CurrentRound = 0;
        TotalSecondsElapsed = 0;
        _callOutDelaySeconds = -1;
        _callOut.Reset();

        StartEnabled = false;
        PauseEnabled = true;
        StopEnabled = true;

        if (_sequence.WaitSeconds > 0)
        {
            _callOutDelaySeconds = 2;
            EnterPhase(TimerPhase.Wait);
        }
        else
        {
            _callOutDelaySeconds = 2;
            EnterPhase(TimerPhase.Work);
        }

        StartTimer();
    }

    private void DoPause()
    {
        if (IsPaused)
        {
            IsPaused = false;
            PauseButtonContent = "⏸  PAUSE";
            _timer?.Start();
        }
        else
        {
            IsPaused = true;
            PauseButtonContent = "▶  RESUME";
            _timer?.Stop();
            _tts.Stop();
        }
    }

    private void DoStop()
    {
        StopTimer();
        _tts.Stop();
        ResetDisplay();
    }

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

    // ── Timer tick ──────────────────────────────────────────────────────────
    private async void Timer_Tick(object? sender, EventArgs e)
    {
        TotalSecondsElapsed++;
        TotalDisplay = FormatTime(TotalSecondsElapsed);

        // Call-out delay countdown
        if (_callOutDelaySeconds > 0)
            _callOutDelaySeconds--;
        else if (_callOutDelaySeconds == 0)
        {
            _callOutDelaySeconds = -1;
            await SpeakExercise();
        }

        // Phase countdown
        PhaseSecondsLeft--;

        // Warning beeps at 3, 2, 1 seconds remaining
        if (WarningBeepEnabled
            && PhaseSecondsLeft >= 1
            && PhaseSecondsLeft <= 3
            && Phase != TimerPhase.Done)
        {
            _audio.PlayWarningBeep();
        }

        if (PhaseSecondsLeft <= 0)
            AdvancePhase();
        else
            CountdownDisplay = FormatTime(PhaseSecondsLeft);
    }

    // ── Phase transitions ───────────────────────────────────────────────────
    private void EnterPhase(TimerPhase phase)
    {
        Phase = phase;
        UpdatePhaseUI(phase);

        switch (phase)
        {
            case TimerPhase.Wait:
                PhaseSecondsLeft = _sequence.WaitSeconds;
                break;

            case TimerPhase.Work:
                CurrentRound++;
                PhaseSecondsLeft = _sequence.WorkSeconds;
                RoundDisplay = $"{CurrentRound} of {_sequence.Repeats}";
                break;

            case TimerPhase.Rest:
                PhaseSecondsLeft = _sequence.RestSeconds;
                _callOutDelaySeconds = 2;
                break;
        }

        CountdownDisplay = FormatTime(PhaseSecondsLeft);
    }

    private void AdvancePhase()
    {
        switch (Phase)
        {
            case TimerPhase.Wait:
                _audio.PlayPhaseEndBeep();
                EnterPhase(TimerPhase.Work);
                break;

            case TimerPhase.Work:
                if (CurrentRound >= _sequence.Repeats)
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
        Phase = TimerPhase.Done;
        StopTimer();

        PhaseLabel = "DONE!";
        PhaseLabelForeground = DoneColorBrush;
        CountdownDisplay = "00:00";
        CountdownForeground = DoneColorBrush;
        RoundDisplay = $"{_sequence.Repeats} of {_sequence.Repeats}";
        ExerciseVisible = false;

        StartEnabled = true;
        PauseEnabled = false;
        StopEnabled = false;
    }

    // ── Phase UI ───────────────────────────────────────────────────────────
    private void UpdatePhaseUI(TimerPhase phase)
    {
        switch (phase)
        {
            case TimerPhase.Idle:
                PhaseLabel = "READY";
                PhaseLabelForeground = WaitColorBrush;
                CountdownForeground = new SolidColorBrush(Colors.WhiteSmoke);
                ExerciseVisible = false;
                break;

            case TimerPhase.Wait:
                PhaseLabel = "WAIT";
                PhaseLabelForeground = WaitColorBrush;
                CountdownForeground = WaitColorBrush;
                ExerciseVisible = false;
                break;

            case TimerPhase.Work:
                PhaseLabel = "WORK";
                PhaseLabelForeground = WorkColorBrush;
                CountdownForeground = WorkColorBrush;
                break;

            case TimerPhase.Rest:
                PhaseLabel = "REST";
                PhaseLabelForeground = RestColorBrush;
                CountdownForeground = RestColorBrush;
                ExerciseVisible = false;
                break;
        }
    }

    // ── TTS call-out ───────────────────────────────────────────────────────
    private async Task SpeakExercise()
    {
        if (_sequence.CallOutMode == CallOutMode.Off) return;

        var exercise = _callOut.Next();
        if (!string.IsNullOrWhiteSpace(exercise))
        {
            ExerciseLabel = exercise.ToUpperInvariant();
            ExerciseVisible = true;
            await _tts.Speak(exercise);
        }
    }

    // ── Display reset ───────────────────────────────────────────────────────
    private void ResetDisplay()
    {
        Phase = TimerPhase.Idle;
        IsPaused = false;
        CurrentRound = 0;
        TotalSecondsElapsed = 0;
        PhaseSecondsLeft = _sequence.RestSeconds;
        _callOutDelaySeconds = -1;

        UpdatePhaseUI(TimerPhase.Idle);
        CountdownDisplay = FormatTime(_sequence.RestSeconds);
        TotalDisplay = "00:00";
        RoundDisplay = $"0 of {_sequence.Repeats}";
        ExerciseLabel = "";
        ExerciseVisible = false;

        StartEnabled = true;
        PauseEnabled = false;
        StopEnabled = false;
        PauseButtonContent = "⏸  PAUSE";
    }

    // ── Helpers ────────────────────────────────────────────────────────────
    private static string FormatTime(int totalSeconds)
    {
        if (totalSeconds < 0) totalSeconds = 0;
        int m = totalSeconds / 60;
        int s = totalSeconds % 60;
        return $"{m:D2}:{s:D2}";
    }

    // ── Cleanup ──────────────────────────────────────────────────────────
    public void Dispose()
    {
        StopTimer();
        _tts.Dispose();
        _audio.Dispose();
        GC.SuppressFinalize(this);
    }
}
