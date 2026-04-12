using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TabataTimer.Services;
using TabataTimer.ViewModels;

namespace TabataTimer.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _vm;

    // Drag state
    private Point _dragStartPoint;
    private object? _draggedItem;
    private Border? _dropHighlight;

    public MainWindow()
    {
        InitializeComponent();

        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TabataTimer");
        var settingsManager = new SettingsManager(Path.Combine(appDataPath, "settings.json"));
        var settings = settingsManager.Load();

        _vm = new MainWindowViewModel(
            settings,
            settingsManager,
            static (existing, appSettings, allSequences) =>
            {
                var dlg = new EditSequenceWindow(existing, appSettings, allSequences, _ => { })
                {
                    Owner = Application.Current.MainWindow
                };
                return dlg.ShowDialog() == true ? dlg.ResultSequence : null;
            },
            (existingName, usedNames) =>
            {
                var dlg = new FolderNameWindow(existingName, usedNames)
                {
                    Owner = this
                };
                return dlg.ShowDialog() == true ? dlg.ResultName : null;
            });

        DataContext = _vm;

        Loaded += (s, e) =>
        {
            if (!double.IsNaN(_vm.CurrentLayout.Left) && !double.IsNaN(_vm.CurrentLayout.Top))
            {
                Left = _vm.CurrentLayout.Left;
                Top = _vm.CurrentLayout.Top;
            }
            Width = Math.Max(_vm.CurrentLayout.Width, MinWidth);
            Height = Math.Max(_vm.CurrentLayout.Height, MinHeight);
        };

        Closing += (s, e) =>
        {
            _vm.CurrentLayout.Left = Left;
            _vm.CurrentLayout.Top = Top;
            _vm.CurrentLayout.Width = Width;
            _vm.CurrentLayout.Height = Height;
            settingsManager.Save(settings);
        };
    }

    // ── Sequence move buttons ────────────────────────────────────────────────

    private void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        if (GetSequenceFromSender(sender) is { } seq)
            _vm.MoveSequenceCommand.Execute((seq, -1));
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        if (GetSequenceFromSender(sender) is { } seq)
            _vm.MoveSequenceCommand.Execute((seq, 1));
    }

    private static TabataSequenceViewModel? GetSequenceFromSender(object sender)
        => sender is Button { DataContext: TabataSequenceViewModel seq } ? seq : null;

    private void MoveFolderUp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: FolderViewModel folder })
            _vm.MoveFolderCommand.Execute((folder, -1));
    }

    private void MoveFolderDown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: FolderViewModel folder })
            _vm.MoveFolderCommand.Execute((folder, 1));
    }

    // ── Drag initiation ──────────────────────────────────────────────────────

    private void Card_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);

        // Don't drag from button clicks
        if (e.OriginalSource is DependencyObject src && IsInsideButton(src))
        {
            _draggedItem = null;
            return;
        }

        _draggedItem = (sender as FrameworkElement)?.DataContext;
    }

    private void Card_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedItem == null) return;

        var pos = e.GetPosition(null);
        var delta = _dragStartPoint - pos;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        var item = _draggedItem;
        _draggedItem = null;
        DragDrop.DoDragDrop((DependencyObject)sender, new DataObject("TabataTimerItem", item), DragDropEffects.Move);
    }

    private void Card_MouseUp(object sender, MouseButtonEventArgs e)
    {
        var item = _draggedItem;
        _draggedItem = null;
        if (item == null) return;

        if (item is FolderViewModel folder)
            _vm.OpenFolderCommand.Execute(folder);
        else if (item is TabataSequenceViewModel seq)
            _vm.StartSequenceCommand.Execute(seq);

        e.Handled = true;
    }

    private static bool IsInsideButton(DependencyObject element)
    {
        var obj = element;
        while (obj != null)
        {
            if (obj is Button) return true;
            obj = VisualTreeHelper.GetParent(obj);
        }
        return false;
    }

    // ── Folder card drop target ──────────────────────────────────────────────

    private void FolderCard_DragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("TabataTimerItem")) { e.Effects = DragDropEffects.None; return; }
        if (sender is Border border)
        {
            _dropHighlight = border;
            border.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x00));
            border.BorderThickness = new Thickness(2);
        }
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void FolderCard_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is Border border)
            ResetFolderHighlight(border);
        e.Handled = true;
    }

    private void FolderCard_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent("TabataTimerItem") ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void FolderCard_Drop(object sender, DragEventArgs e)
    {
        if (_dropHighlight != null) { ResetFolderHighlight(_dropHighlight); _dropHighlight = null; }
        if (!e.Data.GetDataPresent("TabataTimerItem")) return;

        var item = e.Data.GetData("TabataTimerItem");
        if (sender is FrameworkElement { DataContext: FolderViewModel targetVm } && item != null)
        {
            // Prevent dropping a folder onto itself
            if (item is FolderViewModel src && src.Id == targetVm.Id) return;
            _vm.MoveItemToFolder(item, targetVm.Folder);
        }
        e.Handled = true;
    }

    private static void ResetFolderHighlight(Border border)
    {
        border.BorderBrush = new SolidColorBrush(Color.FromRgb(0x2D, 0x35, 0x50));
        border.BorderThickness = new Thickness(1);
    }

    // ── Breadcrumb drop target ───────────────────────────────────────────────

    private void Breadcrumb_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent("TabataTimerItem") ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void Breadcrumb_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("TabataTimerItem")) return;
        var item = e.Data.GetData("TabataTimerItem");
        if (sender is FrameworkElement { DataContext: BreadcrumbItem crumb } && item != null)
            _vm.MoveItemToBreadcrumb(item, crumb.Folder);
        e.Handled = true;
    }
}
