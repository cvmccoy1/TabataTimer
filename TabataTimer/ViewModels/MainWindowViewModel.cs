using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabataTimer.Models;
using TabataTimer.Services;
using TabataTimer.Views;

namespace TabataTimer.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    private readonly ISettingsManager _settingsManager;
    private readonly Func<TabataSequence?, AppSettings, List<TabataSequence>, TabataSequence?> _openEditDialog;

    [ObservableProperty]
    private ObservableCollection<TabataSequenceViewModel> _sequences = new();

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _isEmpty = true;

    public WindowLayout CurrentLayout => _settings.MainWindowLayout;

    public MainWindowViewModel(
        AppSettings settings,
        ISettingsManager settingsManager,
        Func<TabataSequence?, AppSettings, List<TabataSequence>, TabataSequence?> openEditDialog)
    {
        _settings = settings;
        _settingsManager = settingsManager;
        _openEditDialog = openEditDialog;
        LoadSequences();
    }

    public void LoadSequences()
    {
        Sequences.Clear();
        foreach (var seq in _settings.Sequences)
            Sequences.Add(new TabataSequenceViewModel(seq));
        UpdateEmptyState();
    }

    public void OnTimerClosed()
    {
        _settingsManager.Save(_settings);
    }

    private void UpdateEmptyState()
    {
        IsEmpty = Sequences.Count == 0;
        StatusText = IsEmpty
            ? "No sequences — create one to get started"
            : $"{Sequences.Count} sequence{(Sequences.Count == 1 ? "" : "s")} saved";
    }

    private void PersistAndRefresh()
    {
        _settingsManager.Save(_settings);
        LoadSequences();
    }

    [RelayCommand]
    private void NewSequence() => OpenEditDialog(null);

    [RelayCommand]
    private void StartSequence(TabataSequenceViewModel? seqVm)
    {
        if (seqVm == null) return;
        var timerWin = new TimerWindow(seqVm.Model, _settings);
        timerWin.Owner = App.Current.MainWindow;
        timerWin.Closing += (s, e) => _settingsManager.Save(_settings);
        timerWin.ShowDialog();
    }

    [RelayCommand]
    private void EditSequence(TabataSequenceViewModel? seqVm)
    {
        if (seqVm == null) return;
        OpenEditDialog(seqVm.Model);
    }

    [RelayCommand]
    private void DuplicateSequence(TabataSequenceViewModel? seqVm)
    {
        if (seqVm == null) return;
        var seq = seqVm.Model;

        string baseName = "Copy of " + seq.Name;
        var existingNames = _settings.Sequences
            .Select(s => s.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string newName = baseName;
        for (int n = 2; existingNames.Contains(newName); n++)
            newName = $"{baseName} ({n})";

        var clone = new TabataSequence
        {
            Id = Guid.NewGuid(),
            Name = newName,
            WaitSeconds = seq.WaitSeconds,
            Repeats = seq.Repeats,
            WorkSeconds = seq.WorkSeconds,
            RestSeconds = seq.RestSeconds,
            CallOutMode = seq.CallOutMode,
            CallOutList = new List<string>(seq.CallOutList),
            VoiceName = seq.VoiceName
        };

        _settings.Sequences.Add(clone);
        PersistAndRefresh();
    }

    [RelayCommand]
    private void DeleteSequence(TabataSequenceViewModel? seqVm)
    {
        if (seqVm == null) return;
        var result = System.Windows.MessageBox.Show(
            $"Delete sequence \"{seqVm.Name}\"?\n\nThis cannot be undone.",
            "Confirm Delete",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            _settings.Sequences.RemoveAll(s => s.Id == seqVm.Id);
            PersistAndRefresh();
        }
    }

    [RelayCommand]
    private void MoveSequence((TabataSequenceViewModel seq, int direction) request)
    {
        var seq = request.seq;
        int direction = request.direction;
        int idx = _settings.Sequences.FindIndex(s => s.Id == seq.Id);
        int newIdx = idx + direction;
        if (newIdx < 0 || newIdx >= _settings.Sequences.Count) return;

        _settings.Sequences.RemoveAt(idx);
        _settings.Sequences.Insert(newIdx, seq.Model);
        PersistAndRefresh();
    }

    private void OpenEditDialog(TabataSequence? existing)
    {
        var result = _openEditDialog(existing, _settings, _settings.Sequences);
        if (result == null) return;

        if (existing == null)
            _settings.Sequences.Add(result);
        else
        {
            int idx = _settings.Sequences.FindIndex(s => s.Id == existing.Id);
            if (idx >= 0) _settings.Sequences[idx] = result;
        }

        PersistAndRefresh();
    }
}
