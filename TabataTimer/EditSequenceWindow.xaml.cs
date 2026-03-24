using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TabataTimer.Models;
using TabataTimer.Services;
using Windows.Media.SpeechSynthesis;

namespace TabataTimer
{
    public partial class EditSequenceWindow : Window
    {
        private readonly TabataSequence? _original;
        private readonly AppSettings _settings;
        private readonly List<TabataSequence> _allSequences;
        private bool _syncingSlider = false;

        public TabataSequence? ResultSequence { get; private set; }

        public EditSequenceWindow(TabataSequence? existing, AppSettings settings, List<TabataSequence> allSequences)
        {
            _original = existing;
            _settings = settings;
            _allSequences = allSequences;

            InitializeComponent();

            // Set slider bounds from central constants
            WaitSlider.Minimum    = TimerConstraints.WaitMin;
            WaitSlider.Maximum    = TimerConstraints.WaitMax;
            RepeatsSlider.Minimum = TimerConstraints.RepeatsMin;
            RepeatsSlider.Maximum = TimerConstraints.RepeatsMax;
            WorkSlider.Minimum    = TimerConstraints.WorkMin;
            WorkSlider.Maximum    = TimerConstraints.WorkMax;
            RestSlider.Minimum    = TimerConstraints.RestMin;
            RestSlider.Maximum    = TimerConstraints.RestMax;

            // Populate voice list
            var voices = TtsService.GetAvailableVoices();
            VoiceCombo.ItemsSource = voices;

            if (existing != null)
            {
                TitleText.Text   = "EDIT SEQUENCE";
                OkButton.Content = "✓  UPDATE SEQUENCE";
                NameBox.Text     = existing.Name;
                SetSliderAndBox(WaitSlider,    WaitBox,    existing.WaitSeconds);
                SetSliderAndBox(RepeatsSlider, RepeatsBox, existing.Repeats);
                SetSliderAndBox(WorkSlider,    WorkBox,    existing.WorkSeconds);
                SetSliderAndBox(RestSlider,    RestBox,    existing.RestSeconds);

                // Restore call-out mode
                switch (existing.CallOutMode)
                {
                    case CallOutMode.Follow: ModeFollow.IsChecked = true; break;
                    case CallOutMode.Repeat: ModeRepeat.IsChecked = true; break;
                    case CallOutMode.Random: ModeRandom.IsChecked = true; break;
                    default:                 ModeOff.IsChecked    = true; break;
                }

                // Restore voice
                if (!string.IsNullOrEmpty(existing.VoiceName))
                {
                    VoiceCombo.SelectedItem = voices.FirstOrDefault(v => v.DisplayName == existing.VoiceName);
                }
                if (VoiceCombo.SelectedIndex < 0 && voices.Count > 0)
                    VoiceCombo.SelectedIndex = 0;

                // Restore list items
                foreach (var item in existing.CallOutList)
                    AddCallOutRow(item);
            }
            else
            {
                SetSliderAndBox(WaitSlider,    WaitBox,    10);
                SetSliderAndBox(RepeatsSlider, RepeatsBox, 8);
                SetSliderAndBox(WorkSlider,    WorkBox,    20);
                SetSliderAndBox(RestSlider,    RestBox,    10);
                ModeOff.IsChecked = true;
                if (voices.Count > 0)
                    VoiceCombo.SelectedIndex = 0;
            }

            RefreshCallOutUI();
            ValidateForm(null, null);
            NameBox.Focus();

            // Restore window layout
            Loaded += (s, e) => ApplyLayout(GetStoredLayout());
            Closing += (s, e) => SaveLayout();
        }

        private WindowLayout GetStoredLayout()
        {
            var key = _original?.Id ?? Guid.Empty;
            return _settings.EditDialogLayouts.GetValueOrDefault(key) ?? new WindowLayout();
        }

        private void ApplyLayout(WindowLayout layout)
        {
            if (!double.IsNaN(layout.Left) && !double.IsNaN(layout.Top))
            {
                Left = layout.Left;
                Top = layout.Top;
            }
            Width = Math.Max(layout.Width, MinWidth);
            Height = Math.Max(layout.Height, MinHeight);
        }

        private void SaveLayout()
        {
            var key = _original?.Id ?? Guid.Empty;
            _settings.EditDialogLayouts[key] = new WindowLayout
            {
                Left = Left,
                Top = Top,
                Width = Width,
                Height = Height
            };
            // MainWindow will save settings after ShowDialog returns.
        }

        // ── Slider <-> TextBox sync ──────────────────────────────────────────

        private void SetSliderAndBox(Slider slider, TextBox box, int value)
        {
            _syncingSlider = true;
            slider.Value   = Math.Clamp(value, slider.Minimum, slider.Maximum);
            box.Text       = value.ToString();
            _syncingSlider = false;
        }

        private void WaitSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_syncingSlider && WaitBox != null)
            { _syncingSlider = true; WaitBox.Text = ((int)WaitSlider.Value).ToString(); _syncingSlider = false; }
        }
        private void WaitBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_syncingSlider && int.TryParse(WaitBox.Text, out int v))
            { _syncingSlider = true; WaitSlider.Value = Math.Clamp(v, WaitSlider.Minimum, WaitSlider.Maximum); _syncingSlider = false; }
            ValidateForm(sender, e);
        }

        private void RepeatsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_syncingSlider && RepeatsBox != null)
            { _syncingSlider = true; RepeatsBox.Text = ((int)RepeatsSlider.Value).ToString(); _syncingSlider = false; }
            RefreshCallOutRowCount();
        }
        private void RepeatsBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_syncingSlider && int.TryParse(RepeatsBox.Text, out int v))
            { _syncingSlider = true; RepeatsSlider.Value = Math.Clamp(v, RepeatsSlider.Minimum, RepeatsSlider.Maximum); _syncingSlider = false; }
            RefreshCallOutRowCount();
            ValidateForm(sender, e);
        }

        private void WorkSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_syncingSlider && WorkBox != null)
            { _syncingSlider = true; WorkBox.Text = ((int)WorkSlider.Value).ToString(); _syncingSlider = false; }
        }
        private void WorkBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_syncingSlider && int.TryParse(WorkBox.Text, out int v))
            { _syncingSlider = true; WorkSlider.Value = Math.Clamp(v, WorkSlider.Minimum, WorkSlider.Maximum); _syncingSlider = false; }
            ValidateForm(sender, e);
        }

        private void RestSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_syncingSlider && RestBox != null)
            { _syncingSlider = true; RestBox.Text = ((int)RestSlider.Value).ToString(); _syncingSlider = false; }
        }
        private void RestBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_syncingSlider && int.TryParse(RestBox.Text, out int v))
            { _syncingSlider = true; RestSlider.Value = Math.Clamp(v, RestSlider.Minimum, RestSlider.Maximum); _syncingSlider = false; }
            ValidateForm(sender, e);
        }

        private void NumberOnly_PreviewInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^\d+$");
        }

        // ── Call Out UI ──────────────────────────────────────────────────────

        private CallOutMode CurrentMode =>
            ModeFollow.IsChecked == true ? CallOutMode.Follow :
            ModeRepeat.IsChecked == true ? CallOutMode.Repeat :
            ModeRandom.IsChecked == true ? CallOutMode.Random :
            CallOutMode.Off;

        private void CallOutMode_Changed(object sender, RoutedEventArgs e)
        {
            RefreshCallOutUI();
        }

        private void RefreshCallOutUI()
        {
            if (CallOutOffOverlay == null) return;

            var mode = CurrentMode;
            bool isOff = mode == CallOutMode.Off;

            CallOutOffOverlay.Visibility = isOff ? Visibility.Visible : Visibility.Collapsed;
            ListEditButtons.Visibility   = isOff ? Visibility.Collapsed : Visibility.Visible;

            if (!isOff)
                RefreshCallOutRowCount();
        }

        /// <summary>
        /// In Follow mode: list must have exactly Repeats rows.
        /// In Repeat mode: list may have 1..Repeats rows (add/remove freely up to that cap).
        /// In Random mode: list may have any number of rows (no cap).
        /// </summary>
        private void RefreshCallOutRowCount()
        {
            if (CallOutPanel == null) return;
            var mode = CurrentMode;
            if (mode == CallOutMode.Off) return;

            if (!int.TryParse(RepeatsBox?.Text, out int repeats) || repeats < 1)
                return;

            if (mode == CallOutMode.Follow)
            {
                while (CallOutPanel.Children.Count < repeats) AddCallOutRow("");
                while (CallOutPanel.Children.Count > repeats) CallOutPanel.Children.RemoveAt(CallOutPanel.Children.Count - 1);
            }
            else if (mode == CallOutMode.Repeat)
            {
                while (CallOutPanel.Children.Count > repeats)
                    CallOutPanel.Children.RemoveAt(CallOutPanel.Children.Count - 1);
            }
        }

        private void AddCallOutRow(string value = "")
        {
            int index = CallOutPanel.Children.Count + 1;
            var mode  = CurrentMode;

            var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var label = new TextBlock
            {
                Text               = mode == CallOutMode.Random ? "•" : $"{index}.",
                Foreground         = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0)),
                FontSize           = 12,
                VerticalAlignment  = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin             = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(label, 0);

            var tb = new TextBox
            {
                Text        = value,
                Style       = (Style)FindResource("ModernTextBox"),
                FontSize    = 12,
                Padding     = new Thickness(8, 5, 8, 5),
                ToolTip     = "Enter an exercise, or multiple exercises separated by commas"
            };
            Grid.SetColumn(tb, 1);

            row.Children.Add(label);
            row.Children.Add(tb);
            CallOutPanel.Children.Add(row);
        }

        private void AddCallOutItem_Click(object sender, RoutedEventArgs e)
        {
            var mode = CurrentMode;
            if (mode == CallOutMode.Off) return;

            if (!int.TryParse(RepeatsBox.Text, out int repeats)) repeats = TimerConstraints.RepeatsMax;

            if (mode == CallOutMode.Repeat && CallOutPanel.Children.Count >= repeats)
            {
                MessageBox.Show($"Repeat mode allows at most {repeats} items (matching the Repeats setting).",
                    "List Full", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            AddCallOutRow();
        }

        private void RemoveCallOutItem_Click(object sender, RoutedEventArgs e)
        {
            var mode = CurrentMode;
            if (mode == CallOutMode.Off) return;

            if (mode == CallOutMode.Follow)
            {
                if (!int.TryParse(RepeatsBox.Text, out int repeats)) return;
                if (CallOutPanel.Children.Count <= repeats)
                {
                    MessageBox.Show("Follow mode requires exactly one entry per repeat. Reduce the Repeats setting first.",
                        "Cannot Remove", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }

            if (CallOutPanel.Children.Count > 0)
                CallOutPanel.Children.RemoveAt(CallOutPanel.Children.Count - 1);
        }

        private List<string> GetCallOutList()
        {
            var result = new List<string>();
            foreach (var child in CallOutPanel.Children)
            {
                if (child is Grid row)
                {
                    var tb = row.Children.OfType<TextBox>().FirstOrDefault();
                    result.Add(tb?.Text.Trim() ?? "");
                }
            }
            return result;
        }

        // ── Voice Preview ────────────────────────────────────────────────────

        private void TestVoice_Click(object sender, RoutedEventArgs e)
        {
            var voice = VoiceCombo.SelectedItem as VoiceInformation;
            using var preview = new TtsService();
            if (voice != null) preview.SetVoice(voice.DisplayName);
            preview.Speak("This is how this voice sounds.");
        }

        // ── Validation ──────────────────────────────────────────────────────

        private void ValidateForm(object? sender, EventArgs? e)
        {
            if (OkButton == null) return;

            var name   = NameBox.Text.Trim();
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(name))
                errors.Add("Sequence name is required.");
            else
            {
                bool duplicate = _allSequences.Any(s =>
                    s.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                    (_original == null || s.Id != _original.Id));
                if (duplicate)
                    errors.Add($"A sequence named \"{name}\" already exists.");
            }

            if (!TryParseBox(WaitBox,    TimerConstraints.WaitMin,    TimerConstraints.WaitMax,    out _))
                errors.Add($"Wait time must be {TimerConstraints.WaitMin}–{TimerConstraints.WaitMax} seconds.");
            if (!TryParseBox(RepeatsBox, TimerConstraints.RepeatsMin, TimerConstraints.RepeatsMax, out _))
                errors.Add($"Repeats must be {TimerConstraints.RepeatsMin}–{TimerConstraints.RepeatsMax}.");
            if (!TryParseBox(WorkBox,    TimerConstraints.WorkMin,    TimerConstraints.WorkMax,    out _))
                errors.Add($"Work time must be {TimerConstraints.WorkMin}–{TimerConstraints.WorkMax} seconds.");
            if (!TryParseBox(RestBox,    TimerConstraints.RestMin,    TimerConstraints.RestMax,    out _))
                errors.Add($"Rest time must be {TimerConstraints.RestMin}–{TimerConstraints.RestMax} seconds.");

            if (errors.Count > 0)
            {
                ValidationMessage.Text       = errors[0];
                ValidationMessage.Visibility = Visibility.Visible;
                OkButton.IsEnabled           = false;
            }
            else
            {
                ValidationMessage.Visibility = Visibility.Collapsed;
                OkButton.IsEnabled           = true;
            }
        }

        private static bool TryParseBox(TextBox box, int min, int max, out int value)
        {
            if (int.TryParse(box.Text.Trim(), out value) && value >= min && value <= max)
                return true;
            value = 0;
            return false;
        }

        // ── OK / Cancel ──────────────────────────────────────────────────────

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            TryParseBox(WaitBox,    TimerConstraints.WaitMin,    TimerConstraints.WaitMax,    out int wait);
            TryParseBox(RepeatsBox, TimerConstraints.RepeatsMin, TimerConstraints.RepeatsMax, out int repeats);
            TryParseBox(WorkBox,    TimerConstraints.WorkMin,    TimerConstraints.WorkMax,    out int work);
            TryParseBox(RestBox,    TimerConstraints.RestMin,    TimerConstraints.RestMax,    out int rest);

            ResultSequence = new TabataSequence
            {
                Id          = _original?.Id ?? Guid.NewGuid(),
                Name        = NameBox.Text.Trim(),
                WaitSeconds = wait,
                Repeats     = repeats,
                WorkSeconds = work,
                RestSeconds = rest,
                CallOutMode = CurrentMode,
                CallOutList = GetCallOutList(),
                VoiceName   = (VoiceCombo.SelectedItem as VoiceInformation)?.DisplayName
            };

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
