using System.ComponentModel;
using System.Windows;
using TabataTimer.Models;
using TabataTimer.Services;
using TabataTimer.ViewModels;

namespace TabataTimer;

public partial class TimerWindow : Window
{
    private readonly TimerWindowViewModel _vm;

    public TimerWindow(TabataSequence sequence, AppSettings settings)
    {
        var audio = new AudioService();
        var tts = new TtsService();
        var callOut = new CallOutEngine(sequence);

        _vm = new TimerWindowViewModel(sequence, settings, audio, tts, callOut);
        _vm.SettingsChanged += (s, e) => { };

        DataContext = _vm;

        InitializeComponent();

        Loaded += (s, e) =>
        {
            var layout = settings.TimerWindowLayout;
            if (!double.IsNaN(layout.Left) && !double.IsNaN(layout.Top))
            {
                Left = layout.Left;
                Top = layout.Top;
            }
        };

        Closing += (s, e) =>
        {
            settings.TimerWindowLayout = new WindowLayout
            {
                Left = Left,
                Top = Top,
                Width = 0.0,
                Height = 0.0
            };
            _vm.Dispose();
        };

        _vm.ExitRequested += (s, e) => Close();
    }
}
