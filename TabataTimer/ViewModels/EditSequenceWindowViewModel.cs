using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabataTimer.Models;
using TabataTimer.Services;
using TabataTimer.Services.Interfaces;
using Windows.Media.SpeechSynthesis;

namespace TabataTimer.ViewModels;

public partial class CallOutItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _text = "";

    private int _index;
    public int Index
    {
        get => _index;
        set { if (_index != value) { _index = value; OnPropertyChanged(nameof(Index)); } }
    }

    public CallOutItemViewModel(int index, string text = "")
    {
        Index = index;
        _text = text;
    }
}

public partial class EditSequenceWindowViewModel : ObservableObject
{
    private readonly TabataSequence? _original;
    private readonly AppSettings _settings;
    private readonly List<TabataSequence> _allSequences;
    private readonly TtsService _tts;
    private readonly Action<WindowLayout> _saveLayout;

    // ── Available voices ─────────────────────────────────────────────────────
    public IReadOnlyList<VoiceInformation> AvailableVoices { get; }

    // ── Constraints ──────────────────────────────────────────────────────────
    public static int WaitMin => TimerConstraints.WaitMin;
    public static int WaitMax => TimerConstraints.WaitMax;
    public static int RepeatsMin => TimerConstraints.RepeatsMin;
    public static int RepeatsMax => TimerConstraints.RepeatsMax;
    public static int WorkMin => TimerConstraints.WorkMin;
    public static int WorkMax => TimerConstraints.WorkMax;
    public static int RestMin => TimerConstraints.RestMin;
    public static int RestMax => TimerConstraints.RestMax;

    // ── Form fields ───────────────────────────────────────────────────────────
    [ObservableProperty]
    private string _titleText = "NEW SEQUENCE";

    [ObservableProperty]
    private string _okButtonContent = "✓  SAVE SEQUENCE";

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private int _waitSeconds = 10;

    [ObservableProperty]
    private int _repeats = 8;

    [ObservableProperty]
    private int _workSeconds = 20;

    [ObservableProperty]
    private int _restSeconds = 10;

    [ObservableProperty]
    private CallOutMode _callOutMode = CallOutMode.Off;

    [ObservableProperty]
    private VoiceInformation? _selectedVoice;

    [ObservableProperty]
    private double _volume = 0.8;

    [ObservableProperty]
    private bool _warningBeepEnabled = true;

    [ObservableProperty]
    private ObservableCollection<CallOutItemViewModel> _callOutItems = [];

    [ObservableProperty]
    private CallOutItemViewModel? _selectedCallOutItem;

    // ── UI state ─────────────────────────────────────────────────────────────
    [ObservableProperty]
    private bool _isCallOutPanelVisible;

    public bool CanRemoveCallOutItem => IsCallOutPanelVisible && SelectedCallOutItem != null;

    [ObservableProperty]
    private string _validationMessage = "";

    [ObservableProperty]
    private bool _isOkEnabled;

    // ── Result ───────────────────────────────────────────────────────────────
    public TabataSequence? ResultSequence { get; private set; }

    public event EventHandler? OkRequested;
    public event EventHandler? CancelRequested;

    // ── Constructor ─────────────────────────────────────────────────────────
    public EditSequenceWindowViewModel(
        TabataSequence? existing,
        AppSettings settings,
        List<TabataSequence> allSequences,
        Action<WindowLayout> saveLayout)
    {
        _original = existing;
        _settings = settings;
        _allSequences = allSequences;
        _saveLayout = saveLayout;
        _tts = new TtsService();

        AvailableVoices = _tts.GetAvailableVoices();

        if (existing != null)
        {
            TitleText = "EDIT SEQUENCE";
            OkButtonContent = "✓  UPDATE SEQUENCE";
            Name = existing.Name;
            WaitSeconds = existing.WaitSeconds;
            Repeats = existing.Repeats;
            WorkSeconds = existing.WorkSeconds;
            RestSeconds = existing.RestSeconds;
            CallOutMode = existing.CallOutMode;
            Volume = double.IsNaN(existing.Volume) ? 0.8 : existing.Volume;
            WarningBeepEnabled = existing.WarningBeepEnabled;

            if (!string.IsNullOrEmpty(existing.VoiceName))
                SelectedVoice = AvailableVoices.FirstOrDefault(v => v.DisplayName == existing.VoiceName);
            if (SelectedVoice == null && AvailableVoices.Count > 0)
                SelectedVoice = AvailableVoices[0];

            foreach (var item in existing.CallOutList)
                CallOutItems.Add(new CallOutItemViewModel(CallOutItems.Count + 1, item));
        }
        else
        {
            WaitSeconds = 10;
            Repeats = 8;
            WorkSeconds = 20;
            RestSeconds = 10;
            if (AvailableVoices.Count > 0)
                SelectedVoice = AvailableVoices[0];
        }

        IsCallOutPanelVisible = CallOutMode != CallOutMode.Off;
        RefreshValidation();
    }

    // ── Slider sync helpers ─────────────────────────────────────────────────
    partial void OnWaitSecondsChanged(int value) => RefreshValidation();

    partial void OnWorkSecondsChanged(int value) => RefreshValidation();

    partial void OnRestSecondsChanged(int value) => RefreshValidation();

    partial void OnRepeatsChanged(int value) => RefreshValidation();

    partial void OnCallOutModeChanged(CallOutMode value)
    {
        IsCallOutPanelVisible = value != CallOutMode.Off;
        SyncCallOutItemsForCurrentMode();
        RefreshValidation();
    }

    partial void OnNameChanged(string value) => RefreshValidation();

    partial void OnSelectedVoiceChanged(VoiceInformation? value) { /* nothing to validate */ }

    partial void OnSelectedCallOutItemChanged(CallOutItemViewModel? value)
    {
        OnPropertyChanged(nameof(CanRemoveCallOutItem));
    }

    partial void OnIsCallOutPanelVisibleChanged(bool value)
    {
        if (!value) SelectedCallOutItem = null;
        OnPropertyChanged(nameof(CanRemoveCallOutItem));
    }

    // ── Call-out list management ────────────────────────────────────────────
    private void SyncCallOutItemsForCurrentMode()
    {
        if (CallOutMode == CallOutMode.Off) return;

        // Repeat mode: enforce max of Repeats items
        if (CallOutMode == CallOutMode.Repeat)
        {
            while (CallOutItems.Count > Repeats)
                CallOutItems.RemoveAt(CallOutItems.Count - 1);
        }
        // Follow and Random modes: no auto-sync, user adds/removes as needed.
    }

    private void ReindexCallOutItems()
    {
        for (int i = 0; i < CallOutItems.Count; i++)
            CallOutItems[i].Index = i + 1;
    }

    [RelayCommand]
    private void AddCallOutItem()
    {
        if (CallOutMode == CallOutMode.Off) return;

        int insertIndex = CallOutItems.Count;

        if (CallOutMode == CallOutMode.Repeat)
        {
            if (CallOutItems.Count >= Repeats)
            {
                MessageBox.Show(
                    $"Repeat mode allows at most {Repeats} items (matching the Repeats setting).",
                    "List Full", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (SelectedCallOutItem != null)
                insertIndex = CallOutItems.IndexOf(SelectedCallOutItem) + 1;
        }
        else if (SelectedCallOutItem != null)
        {
            insertIndex = CallOutItems.IndexOf(SelectedCallOutItem) + 1;
        }

        CallOutItems.Insert(insertIndex, new CallOutItemViewModel(insertIndex + 1));
        ReindexCallOutItems();
        SelectedCallOutItem = CallOutItems[insertIndex];
    }

    [RelayCommand]
    private void RemoveCallOutItem()
    {
        if (CallOutMode == CallOutMode.Off) return;
        if (SelectedCallOutItem == null) return;

        if (CallOutMode == CallOutMode.Repeat && CallOutItems.Count <= 1)
        {
            MessageBox.Show(
                "Repeat mode requires at least one entry.",
                "Cannot Remove", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var toRemove = SelectedCallOutItem;
        SelectedCallOutItem = null;
        CallOutItems.Remove(toRemove);
        ReindexCallOutItems();
    }

    [RelayCommand]
    private async Task TestVoice()
    {
        var preview = new TtsService();
        if (SelectedVoice != null) preview.SetVoice(SelectedVoice.DisplayName);
        await preview.Speak("This is how this voice sounds.");
        preview.Dispose();
    }

    // ── Validation ──────────────────────────────────────────────────────────
    private void RefreshValidation()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Sequence name is required.");
        else
        {
            bool duplicate = _allSequences.Any(s =>
                s.Name.Equals(Name, StringComparison.OrdinalIgnoreCase) &&
                (_original == null || s.Id != _original.Id));
            if (duplicate)
                errors.Add($"A sequence named \"{Name}\" already exists.");
        }

        if (WaitSeconds < WaitMin || WaitSeconds > WaitMax)
            errors.Add($"Wait time must be {WaitMin}–{WaitMax} seconds.");
        if (Repeats < RepeatsMin || Repeats > RepeatsMax)
            errors.Add($"Repeats must be {RepeatsMin}–{RepeatsMax}.");
        if (WorkSeconds < WorkMin || WorkSeconds > WorkMax)
            errors.Add($"Work time must be {WorkMin}–{WorkMax} seconds.");
        if (RestSeconds < RestMin || RestSeconds > RestMax)
            errors.Add($"Rest time must be {RestMin}–{RestMax} seconds.");

        ValidationMessage = errors.FirstOrDefault() ?? "";
        IsOkEnabled = errors.Count == 0;
    }

    // ── OK / Cancel ─────────────────────────────────────────────────────────
    [RelayCommand]
    private void Ok()
    {
        ResultSequence = new TabataSequence
        {
            Id = _original?.Id ?? Guid.NewGuid(),
            Name = Name.Trim(),
            WaitSeconds = WaitSeconds,
            Repeats = Repeats,
            WorkSeconds = WorkSeconds,
            RestSeconds = RestSeconds,
            CallOutMode = CallOutMode,
            CallOutList = [.. CallOutItems.Select(i => i.Text.Trim())],
            VoiceName = SelectedVoice?.DisplayName,
            Volume = Volume,
            WarningBeepEnabled = WarningBeepEnabled
        };
        OkRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel()
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }

    public WindowLayout GetLayout()
    {
        var key = _original?.Id ?? Guid.Empty;
        return _settings.EditDialogLayouts.GetValueOrDefault(key) ?? new WindowLayout();
    }

    public void ApplyLayout(Window window)
    {
        var layout = GetLayout();
        if (!double.IsNaN(layout.Left) && !double.IsNaN(layout.Top))
        {
            window.Left = layout.Left;
            window.Top = layout.Top;
        }
        window.Width = Math.Max(layout.Width, window.MinWidth);
        window.Height = Math.Max(layout.Height, window.MinHeight);
    }

    public void CaptureAndSaveLayout(Window window)
    {
        var key = _original?.Id ?? Guid.Empty;
        _settings.EditDialogLayouts[key] = new WindowLayout
        {
            Left = window.Left,
            Top = window.Top,
            Width = window.Width,
            Height = window.Height
        };
        _saveLayout(_settings.EditDialogLayouts[key]);
    }
}
