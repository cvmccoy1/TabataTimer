using System.Windows;
using TabataTimer.Models;
using TabataTimer.Services;
using TabataTimer.ViewModels;

namespace TabataTimer;

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
}
