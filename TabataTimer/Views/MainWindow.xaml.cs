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
    private int _insertionIndex = -1;

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
        // Hide the reorder indicator — folder card shows its own drop highlight instead
        InsertionIndicator.Visibility = Visibility.Collapsed;
        _insertionIndex = -1;

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

    // ── Reorder drop target (ItemsGrid) ─────────────────────────────────────

    private void ItemsGrid_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("TabataTimerItem")) { e.Effects = DragDropEffects.None; return; }

        var panel = FindVisualChild<StackPanel>(ItemsList);
        if (panel == null || panel.Children.Count == 0)
        {
            InsertionIndicator.Visibility = Visibility.Collapsed;
            e.Effects = DragDropEffects.Move;
            return;
        }

        var item = e.Data.GetData("TabataTimerItem");
        int mouseIdx = CalcInsertionIndex(panel, e.GetPosition(panel).Y);

        // Clamp to the valid section for this item type
        int folderCount = _vm.CurrentItems.OfType<FolderViewModel>().Count();
        int seqCount    = _vm.CurrentItems.OfType<TabataSequenceViewModel>().Count();
        int validIdx;
        if (item is FolderViewModel)
            validIdx = Math.Clamp(mouseIdx, 0, folderCount);
        else if (item is TabataSequenceViewModel)
            validIdx = Math.Clamp(mouseIdx, folderCount, folderCount + seqCount);
        else
            validIdx = -1;

        // Suppress indicator when the drop would be a no-op (same position)
        if (validIdx >= 0)
        {
            int currentPos = GetCurrentItemsIndex(item);
            if (currentPos >= 0 && (validIdx == currentPos || validIdx == currentPos + 1))
                validIdx = -1;
        }

        if (validIdx >= 0)
        {
            _insertionIndex = validIdx;
            double gapY   = GetIndicatorY(panel, validIdx);
            var    gridPt = panel.TranslatePoint(new Point(0, gapY), ItemsGrid);
            InsertionIndicator.Margin     = new Thickness(16, gridPt.Y - 1.5, 16, 0);
            InsertionIndicator.Visibility = Visibility.Visible;
        }
        else
        {
            _insertionIndex               = -1;
            InsertionIndicator.Visibility = Visibility.Collapsed;
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void ItemsGrid_DragLeave(object sender, DragEventArgs e)
    {
        InsertionIndicator.Visibility = Visibility.Collapsed;
        _insertionIndex = -1;
    }

    private void ItemsGrid_Drop(object sender, DragEventArgs e)
    {
        InsertionIndicator.Visibility = Visibility.Collapsed;

        if (_insertionIndex >= 0 && e.Data.GetDataPresent("TabataTimerItem"))
        {
            var item = e.Data.GetData("TabataTimerItem");
            if (item != null)
                _vm.ReorderItem(item, _insertionIndex);
            e.Handled = true;
        }

        _insertionIndex = -1;
    }

    // ── Reorder helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns the insertion index (0 = before first item, Count = after last)
    /// for a given Y coordinate in the panel's own coordinate space.
    /// </summary>
    private static int CalcInsertionIndex(StackPanel panel, double mouseY)
    {
        for (int i = 0; i < panel.Children.Count; i++)
        {
            if (panel.Children[i] is not FrameworkElement child) continue;
            var pos = child.TranslatePoint(new Point(0, 0), panel);
            if (mouseY < pos.Y + child.ActualHeight / 2)
                return i;
        }
        return panel.Children.Count;
    }

    /// <summary>
    /// Returns the Y coordinate (in the panel's coordinate space) at which to
    /// draw the insertion indicator for the given insertion index.
    /// </summary>
    private static double GetIndicatorY(StackPanel panel, int index)
    {
        int count = panel.Children.Count;
        if (count == 0) return 0;

        if (index <= 0)
        {
            var first = panel.Children[0] as FrameworkElement;
            return first?.TranslatePoint(new Point(0, 0), panel).Y ?? 0;
        }

        if (index >= count)
        {
            var last = panel.Children[count - 1] as FrameworkElement;
            if (last != null)
            {
                var p = last.TranslatePoint(new Point(0, 0), panel);
                return p.Y + last.ActualHeight;
            }
            return 0;
        }

        // Midpoint of the gap between the item above and the item below
        var above = panel.Children[index - 1] as FrameworkElement;
        var below = panel.Children[index]     as FrameworkElement;
        if (above != null && below != null)
        {
            var ap = above.TranslatePoint(new Point(0, 0), panel);
            var bp = below.TranslatePoint(new Point(0, 0), panel);
            return (ap.Y + above.ActualHeight + bp.Y) / 2;
        }
        return 0;
    }

    /// <summary>Index of <paramref name="item"/> in CurrentItems (-1 if not found).</summary>
    private int GetCurrentItemsIndex(object? item)
    {
        if (item == null) return -1;
        var items = _vm.CurrentItems;
        for (int i = 0; i < items.Count; i++)
        {
            if (item is FolderViewModel fvm && items[i] is FolderViewModel f && f.Id == fvm.Id) return i;
            if (item is TabataSequenceViewModel svm && items[i] is TabataSequenceViewModel s && s.Id == svm.Id) return i;
        }
        return -1;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            var result = FindVisualChild<T>(child);
            if (result != null) return result;
        }
        return null;
    }
}
