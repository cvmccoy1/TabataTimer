using System.IO;
using System.Windows;
using System.Windows.Controls;
using TabataTimer.Models;
using TabataTimer.Services;
using TabataTimer.ViewModels;

namespace TabataTimer.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _vm;

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
                static (existing, settings, allSequences) =>
                {
                    var dlg = new EditSequenceWindow(existing, settings, allSequences, _ => { });
                    dlg.Owner = System.Windows.Application.Current.MainWindow;
                    return dlg.ShowDialog() == true ? dlg.ResultSequence : null;
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

        private TabataSequenceViewModel? GetSequenceFromSender(object sender)
        {
            if (sender is Button btn && btn.DataContext is TabataSequenceViewModel seq)
                return seq;
            return null;
        }
    }
}
