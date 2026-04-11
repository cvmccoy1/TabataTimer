using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabataTimer.Models;
using TabataTimer.Services.Interfaces;
using TabataTimer.Views;

namespace TabataTimer.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    private readonly ISettingsManager _settingsManager;
    private readonly Func<TabataSequence?, AppSettings, List<TabataSequence>, TabataSequence?> _openEditDialog;
    private readonly Func<string?, IEnumerable<string>, string?> _openFolderNameDialog;

    // Navigation path — empty means we are at root
    private readonly List<SequenceFolder> _folderPath = [];

    [ObservableProperty] private ObservableCollection<object> _currentItems = [];
    [ObservableProperty] private ObservableCollection<BreadcrumbItem> _breadcrumbs = [];
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private bool _isEmpty = true;
    [ObservableProperty] private bool _isAtRoot = true;

    public WindowLayout CurrentLayout => _settings.MainWindowLayout;

    public MainWindowViewModel(
        AppSettings settings,
        ISettingsManager settingsManager,
        Func<TabataSequence?, AppSettings, List<TabataSequence>, TabataSequence?> openEditDialog,
        Func<string?, IEnumerable<string>, string?> openFolderNameDialog)
    {
        _settings = settings;
        _settingsManager = settingsManager;
        _openEditDialog = openEditDialog;
        _openFolderNameDialog = openFolderNameDialog;
        Refresh();
    }

    // ── Current-level accessors ──────────────────────────────────────────────

    private SequenceFolder? CurrentFolder => _folderPath.Count > 0 ? _folderPath[^1] : null;
    private List<TabataSequence> CurrentSequences => CurrentFolder?.Sequences ?? _settings.Sequences;
    private List<SequenceFolder> CurrentSubFolders => CurrentFolder?.SubFolders ?? _settings.RootFolders;

    // ── Refresh ──────────────────────────────────────────────────────────────

    public void Refresh()
    {
        RefreshCurrentItems();
        RefreshBreadcrumbs();
        UpdateStatus();
    }

    private void RefreshCurrentItems()
    {
        CurrentItems.Clear();
        foreach (var folder in CurrentSubFolders)
            CurrentItems.Add(new FolderViewModel(folder));
        foreach (var seq in CurrentSequences)
            CurrentItems.Add(new TabataSequenceViewModel(seq));
    }

    private void RefreshBreadcrumbs()
    {
        Breadcrumbs.Clear();
        Breadcrumbs.Add(new BreadcrumbItem("Home", null, isFirst: true,
            new RelayCommand(() => NavigateTo(null))));

        foreach (var folder in _folderPath)
        {
            var captured = folder;
            Breadcrumbs.Add(new BreadcrumbItem(captured.Name, captured, isFirst: false,
                new RelayCommand(() => NavigateTo(captured))));
        }

        IsAtRoot = _folderPath.Count == 0;
    }

    private void NavigateTo(SequenceFolder? target)
    {
        if (target == null)
        {
            _folderPath.Clear();
        }
        else
        {
            int idx = _folderPath.IndexOf(target);
            if (idx >= 0)
                _folderPath.RemoveRange(idx + 1, _folderPath.Count - idx - 1);
        }
        Refresh();
    }

    private void UpdateStatus()
    {
        int folders = CurrentSubFolders.Count;
        int seqs = CurrentSequences.Count;
        IsEmpty = folders == 0 && seqs == 0;

        if (IsEmpty)
        {
            StatusText = "Empty — create a sequence or folder";
            return;
        }

        var parts = new List<string>();
        if (folders > 0) parts.Add($"{folders} folder{(folders == 1 ? "" : "s")}");
        if (seqs > 0) parts.Add($"{seqs} sequence{(seqs == 1 ? "" : "s")}");
        StatusText = string.Join(", ", parts);
    }

    private void PersistAndRefresh()
    {
        _settingsManager.Save(_settings);
        Refresh();
    }

    public void OnTimerClosed() => _settingsManager.Save(_settings);

    // ── Folder commands ──────────────────────────────────────────────────────

    [RelayCommand]
    private void OpenFolder(FolderViewModel? vm)
    {
        if (vm == null) return;
        _folderPath.Add(vm.Folder);
        Refresh();
    }

    [RelayCommand]
    private void Back()
    {
        if (_folderPath.Count > 0)
        {
            _folderPath.RemoveAt(_folderPath.Count - 1);
            Refresh();
        }
    }

    [RelayCommand]
    private void NewFolder()
    {
        var name = _openFolderNameDialog(null, CurrentSubFolders.Select(f => f.Name));
        if (string.IsNullOrWhiteSpace(name)) return;
        CurrentSubFolders.Add(new SequenceFolder { Name = name });
        PersistAndRefresh();
    }

    [RelayCommand]
    private void RenameFolder(FolderViewModel? vm)
    {
        if (vm == null) return;
        var usedNames = CurrentSubFolders.Where(f => f.Id != vm.Id).Select(f => f.Name);
        var name = _openFolderNameDialog(vm.Name, usedNames);
        if (string.IsNullOrWhiteSpace(name)) return;
        vm.Folder.Name = name;
        PersistAndRefresh();
    }

    [RelayCommand]
    private void CopyFolder(FolderViewModel? vm)
    {
        if (vm == null) return;
        var usedNames = CurrentSubFolders.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        string baseName = "Copy of " + vm.Name;
        string proposed = baseName;
        for (int n = 2; usedNames.Contains(proposed); n++)
            proposed = $"{baseName} ({n})";

        var name = _openFolderNameDialog(proposed, usedNames);
        if (string.IsNullOrWhiteSpace(name)) return;
        CurrentSubFolders.Add(CloneFolder(vm.Folder, name));
        PersistAndRefresh();
    }

    [RelayCommand]
    private void DeleteFolder(FolderViewModel? vm)
    {
        if (vm == null) return;
        int inner = CountContents(vm.Folder);
        string detail = inner > 0 ? $"\n\nThis will also delete {inner} item{(inner == 1 ? "" : "s")} inside." : "";
        var res = System.Windows.MessageBox.Show(
            $"Delete folder \"{vm.Name}\"?{detail}\n\nThis cannot be undone.",
            "Confirm Delete",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (res == System.Windows.MessageBoxResult.Yes)
        {
            CurrentSubFolders.Remove(vm.Folder);
            PersistAndRefresh();
        }
    }

    // ── Sequence commands ────────────────────────────────────────────────────

    [RelayCommand]
    private void NewSequence() => OpenEditDialog(null);

    [RelayCommand]
    private void StartSequence(TabataSequenceViewModel? vm)
    {
        if (vm == null) return;
        var timerWin = new TimerWindow(vm.Model, _settings) { Owner = App.Current.MainWindow };
        timerWin.Closing += (s, e) => _settingsManager.Save(_settings);
        timerWin.ShowDialog();
    }

    [RelayCommand]
    private void EditSequence(TabataSequenceViewModel? vm)
    {
        if (vm == null) return;
        OpenEditDialog(vm.Model);
    }

    [RelayCommand]
    private void DuplicateSequence(TabataSequenceViewModel? vm)
    {
        if (vm == null) return;
        var seq = vm.Model;
        string baseName = "Copy of " + seq.Name;
        var used = CurrentSequences.Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        string newName = baseName;
        for (int n = 2; used.Contains(newName); n++)
            newName = $"{baseName} ({n})";

        CurrentSequences.Add(new TabataSequence
        {
            Id = Guid.NewGuid(),
            Name = newName,
            WaitSeconds = seq.WaitSeconds,
            Repeats = seq.Repeats,
            WorkSeconds = seq.WorkSeconds,
            RestSeconds = seq.RestSeconds,
            CallOutMode = seq.CallOutMode,
            CallOutList = [.. seq.CallOutList],
            VoiceName = seq.VoiceName,
            Volume = seq.Volume,
            WarningBeepEnabled = seq.WarningBeepEnabled
        });
        PersistAndRefresh();
    }

    [RelayCommand]
    private void DeleteSequence(TabataSequenceViewModel? vm)
    {
        if (vm == null) return;
        var res = System.Windows.MessageBox.Show(
            $"Delete sequence \"{vm.Name}\"?\n\nThis cannot be undone.",
            "Confirm Delete",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (res == System.Windows.MessageBoxResult.Yes)
        {
            CurrentSequences.RemoveAll(s => s.Id == vm.Id);
            PersistAndRefresh();
        }
    }

    [RelayCommand]
    private void MoveSequence((TabataSequenceViewModel seq, int direction) req)
    {
        int idx = CurrentSequences.FindIndex(s => s.Id == req.seq.Id);
        int newIdx = idx + req.direction;
        if (newIdx < 0 || newIdx >= CurrentSequences.Count) return;
        var model = CurrentSequences[idx];
        CurrentSequences.RemoveAt(idx);
        CurrentSequences.Insert(newIdx, model);
        PersistAndRefresh();
    }

    [RelayCommand]
    private void MoveFolder((FolderViewModel folder, int direction) req)
    {
        int idx = CurrentSubFolders.FindIndex(f => f.Id == req.folder.Id);
        int newIdx = idx + req.direction;
        if (newIdx < 0 || newIdx >= CurrentSubFolders.Count) return;
        var model = CurrentSubFolders[idx];
        CurrentSubFolders.RemoveAt(idx);
        CurrentSubFolders.Insert(newIdx, model);
        PersistAndRefresh();
    }

    // ── Drag-drop moves ──────────────────────────────────────────────────────

    /// <summary>Drop an item onto a folder card at the current level.</summary>
    public void MoveItemToFolder(object item, SequenceFolder targetFolder)
    {
        if (item is FolderViewModel fvm)
        {
            if (fvm.Id == targetFolder.Id) return;
            CurrentSubFolders.Remove(fvm.Folder);
            targetFolder.SubFolders.Add(fvm.Folder);
        }
        else if (item is TabataSequenceViewModel svm)
        {
            CurrentSequences.Remove(svm.Model);
            targetFolder.Sequences.Add(svm.Model);
        }
        PersistAndRefresh();
    }

    /// <summary>Drop an item onto a breadcrumb (targetFolder null = root).</summary>
    public void MoveItemToBreadcrumb(object item, SequenceFolder? targetFolder)
    {
        // Moving to current level is a no-op
        if (targetFolder == CurrentFolder) return;

        var destFolders = targetFolder?.SubFolders ?? _settings.RootFolders;
        var destSequences = targetFolder?.Sequences ?? _settings.Sequences;

        if (item is FolderViewModel fvm)
        {
            // Prevent moving into own ancestor
            if (IsAncestorOf(fvm.Folder, targetFolder)) return;
            CurrentSubFolders.Remove(fvm.Folder);
            destFolders.Add(fvm.Folder);
        }
        else if (item is TabataSequenceViewModel svm)
        {
            CurrentSequences.Remove(svm.Model);
            destSequences.Add(svm.Model);
        }
        PersistAndRefresh();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void OpenEditDialog(TabataSequence? existing)
    {
        var result = _openEditDialog(existing, _settings, CurrentSequences);
        if (result == null) return;

        if (existing == null)
            CurrentSequences.Add(result);
        else
        {
            int idx = CurrentSequences.FindIndex(s => s.Id == existing.Id);
            if (idx >= 0) CurrentSequences[idx] = result;
        }
        PersistAndRefresh();
    }

    private static SequenceFolder CloneFolder(SequenceFolder src, string newName) => new()
    {
        Id = Guid.NewGuid(),
        Name = newName,
        Sequences = src.Sequences.Select(s => new TabataSequence
        {
            Id = Guid.NewGuid(),
            Name = s.Name,
            WaitSeconds = s.WaitSeconds,
            Repeats = s.Repeats,
            WorkSeconds = s.WorkSeconds,
            RestSeconds = s.RestSeconds,
            CallOutMode = s.CallOutMode,
            CallOutList = [.. s.CallOutList],
            VoiceName = s.VoiceName,
            Volume = s.Volume,
            WarningBeepEnabled = s.WarningBeepEnabled
        }).ToList(),
        SubFolders = src.SubFolders.Select(f => CloneFolder(f, f.Name)).ToList()
    };

    private static int CountContents(SequenceFolder f)
    {
        int n = f.Sequences.Count + f.SubFolders.Count;
        foreach (var sub in f.SubFolders) n += CountContents(sub);
        return n;
    }

    private static bool IsAncestorOf(SequenceFolder candidate, SequenceFolder? target)
    {
        if (target == null) return false;
        if (candidate.Id == target.Id) return true;
        return candidate.SubFolders.Any(sub => IsAncestorOf(sub, target));
    }
}
