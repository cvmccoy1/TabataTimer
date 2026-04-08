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
