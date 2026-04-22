using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TabataTimer.Models;
using TabataTimer.ViewModels;

namespace TabataTimer.Views;

public partial class EditSequenceWindow : Window
{
    private readonly EditSequenceWindowViewModel _vm;

    public TabataSequence? ResultSequence => _vm.ResultSequence;

    // Call-out drag state
    private Point _callOutDragStart;
    private CallOutItemViewModel? _callOutDraggedItem;
    private int _callOutInsertionIndex = -1;

    public EditSequenceWindow(
        TabataSequence? existing,
        AppSettings settings,
        List<TabataSequence> allSequences,
        Action<WindowLayout> saveLayout)
    {
        _vm = new EditSequenceWindowViewModel(existing, settings, allSequences, saveLayout);
        DataContext = _vm;

        InitializeComponent();

        Loaded += (s, e) => _vm.ApplyLayout(this);
        Closing += (s, e) => _vm.CaptureAndSaveLayout(this);

        _vm.CallOutItems.CollectionChanged += CallOutItems_CollectionChanged;

        _vm.OkRequested += (s, e) =>
        {
            DialogResult = true;
            Close();
        };

        _vm.CancelRequested += (s, e) =>
        {
            DialogResult = false;
            Close();
        };
    }

    private void CallOutTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is CallOutItemViewModel item)
        {
            if (FindParent<ListBoxItem>(tb) is { } listBoxItem)
                listBoxItem.IsSelected = true;
        }
    }

    private void CallOutItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
        {
            var newItem = e.NewItems[0];
            Dispatcher.BeginInvoke(() =>
            {
                var listBox = FindVisualChild<ListBox>(this);
                if (listBox == null) return;
                var container = listBox.ItemContainerGenerator.ContainerFromItem(newItem) as ListBoxItem;
                container ??= listBox.ItemContainerGenerator.ContainerFromIndex(listBox.Items.Count - 1) as ListBoxItem;
                if (container == null) return;
                container.IsSelected = true;
                var textBox = FindVisualChild<TextBox>(container);
                textBox?.Focus();
            }, System.Windows.Threading.DispatcherPriority.Background);
        }
    }

    // ── Call-out drag initiation ─────────────────────────────────────────────

    private void CallOutRow_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _callOutDragStart = e.GetPosition(null);
        // Only allow dragging from the drag handle / index label, not from the TextBox
        if (e.OriginalSource is DependencyObject src && FindParent<TextBox>(src) != null)
        {
            _callOutDraggedItem = null;
            return;
        }
        _callOutDraggedItem = (sender as FrameworkElement)?.DataContext as CallOutItemViewModel;
    }

    private void CallOutRow_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _callOutDraggedItem == null) return;
        var pos = e.GetPosition(null);
        var delta = _callOutDragStart - pos;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        var item = _callOutDraggedItem;
        _callOutDraggedItem = null;
        DragDrop.DoDragDrop((DependencyObject)sender, new DataObject("CallOutItem", item), DragDropEffects.Move);
    }

    private void CallOutRow_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _callOutDraggedItem = null;
    }

    // ── Call-out drop target ─────────────────────────────────────────────────

    private void CallOutListGrid_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("CallOutItem")) { e.Effects = DragDropEffects.None; return; }

        var panel = FindVisualChild<VirtualizingStackPanel>(CallOutListBox)
                    ?? (System.Windows.Controls.Panel?)FindVisualChild<StackPanel>(CallOutListBox);
        if (panel == null || panel.Children.Count == 0)
        {
            CallOutInsertionIndicator.Visibility = Visibility.Collapsed;
            e.Effects = DragDropEffects.Move;
            return;
        }

        var item = e.Data.GetData("CallOutItem") as CallOutItemViewModel;
        int mouseIdx = CalcInsertionIndex(panel, e.GetPosition(panel).Y);

        int currentPos = item != null ? _vm.CallOutItems.IndexOf(item) : -1;
        bool noOp = currentPos >= 0 && (mouseIdx == currentPos || mouseIdx == currentPos + 1);

        if (!noOp)
        {
            _callOutInsertionIndex = mouseIdx;
            double gapY  = GetIndicatorY(panel, mouseIdx);
            var    gridPt = panel.TranslatePoint(new System.Windows.Point(0, gapY), CallOutListGrid);
            CallOutInsertionIndicator.Margin     = new Thickness(8, gridPt.Y - 1.5, 8, 0);
            CallOutInsertionIndicator.Visibility = Visibility.Visible;
        }
        else
        {
            _callOutInsertionIndex               = -1;
            CallOutInsertionIndicator.Visibility = Visibility.Collapsed;
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void CallOutListGrid_DragLeave(object sender, DragEventArgs e)
    {
        CallOutInsertionIndicator.Visibility = Visibility.Collapsed;
        _callOutInsertionIndex = -1;
    }

    private void CallOutListGrid_Drop(object sender, DragEventArgs e)
    {
        CallOutInsertionIndicator.Visibility = Visibility.Collapsed;
        if (_callOutInsertionIndex >= 0 && e.Data.GetDataPresent("CallOutItem"))
        {
            var item = e.Data.GetData("CallOutItem") as CallOutItemViewModel;
            if (item != null)
                _vm.MoveCallOutItem(item, _callOutInsertionIndex);
            e.Handled = true;
        }
        _callOutInsertionIndex = -1;
    }

    // ── Reorder helpers ──────────────────────────────────────────────────────

    private static int CalcInsertionIndex(System.Windows.Controls.Panel panel, double mouseY)
    {
        for (int i = 0; i < panel.Children.Count; i++)
        {
            if (panel.Children[i] is not FrameworkElement child) continue;
            var pos = child.TranslatePoint(new System.Windows.Point(0, 0), panel);
            if (mouseY < pos.Y + child.ActualHeight / 2)
                return i;
        }
        return panel.Children.Count;
    }

    private static double GetIndicatorY(System.Windows.Controls.Panel panel, int index)
    {
        int count = panel.Children.Count;
        if (count == 0) return 0;

        if (index <= 0)
        {
            var first = panel.Children[0] as FrameworkElement;
            return first?.TranslatePoint(new System.Windows.Point(0, 0), panel).Y ?? 0;
        }

        if (index >= count)
        {
            var last = panel.Children[count - 1] as FrameworkElement;
            if (last != null)
            {
                var p = last.TranslatePoint(new System.Windows.Point(0, 0), panel);
                return p.Y + last.ActualHeight;
            }
            return 0;
        }

        var above = panel.Children[index - 1] as FrameworkElement;
        var below = panel.Children[index]     as FrameworkElement;
        if (above != null && below != null)
        {
            var ap = above.TranslatePoint(new System.Windows.Point(0, 0), panel);
            var bp = below.TranslatePoint(new System.Windows.Point(0, 0), panel);
            return (ap.Y + above.ActualHeight + bp.Y) / 2;
        }
        return 0;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
                return typedChild;
            if (FindVisualChild<T>(child) is { } descendant)
                return descendant;
        }
        return null;
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
        while (parent != null)
        {
            if (parent is T typedParent)
                return typedParent;
            parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
        }
        return null;
    }
}
