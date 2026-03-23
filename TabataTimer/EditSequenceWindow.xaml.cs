using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TabataTimer.Models;

namespace TabataTimer
{
    public partial class EditSequenceWindow : Window
    {
        private readonly TabataSequence? _original;
        private readonly List<TabataSequence> _allSequences;
        private bool _syncingSlider = false;

        public TabataSequence? ResultSequence { get; private set; }

        public EditSequenceWindow(TabataSequence? existing, List<TabataSequence> allSequences)
        {
            InitializeComponent();
            _allSequences = allSequences;
            _original = existing;

            if (existing != null)
            {
                TitleText.Text = "EDIT SEQUENCE";
                OkButton.Content = "✓  UPDATE SEQUENCE";
                NameBox.Text = existing.Name;
                SetSliderAndBox(WaitSlider, WaitBox, existing.WaitSeconds);
                SetSliderAndBox(RepeatsSlider, RepeatsBox, existing.Repeats);
                SetSliderAndBox(WorkSlider, WorkBox, existing.WorkSeconds);
                SetSliderAndBox(RestSlider, RestBox, existing.RestSeconds);
            }
            else
            {
                SetSliderAndBox(WaitSlider, WaitBox, 10);
                SetSliderAndBox(RepeatsSlider, RepeatsBox, 8);
                SetSliderAndBox(WorkSlider, WorkBox, 20);
                SetSliderAndBox(RestSlider, RestBox, 10);
            }

            ValidateForm(null, null);
            NameBox.Focus();
        }

        private void SetSliderAndBox(Slider slider, TextBox box, int value)
        {
            _syncingSlider = true;
            slider.Value = Math.Clamp(value, slider.Minimum, slider.Maximum);
            box.Text = value.ToString();
            _syncingSlider = false;
        }

        private void ValidateForm(object? sender, EventArgs? e)
        {
            if (OkButton == null) return;

            var name = NameBox.Text.Trim();
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(name))
                errors.Add("Sequence name is required.");
            else
            {
                // Duplicate check (exclude self when editing)
                bool duplicate = _allSequences.Any(s =>
                    s.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                    (_original == null || s.Id != _original.Id));
                if (duplicate)
                    errors.Add($"A sequence named \"{name}\" already exists.");
            }

            if (!TryParseBox(WaitBox, 0, 3600, out _))
                errors.Add("Wait time must be 0–3600 seconds.");
            if (!TryParseBox(RepeatsBox, 1, 999, out _))
                errors.Add("Repeats must be 1–999.");
            if (!TryParseBox(WorkBox, 1, 3600, out _))
                errors.Add("Work time must be 1–3600 seconds.");
            if (!TryParseBox(RestBox, 1, 3600, out _))
                errors.Add("Rest time must be 1–3600 seconds.");

            if (errors.Count > 0)
            {
                ValidationMessage.Text = errors[0];
                ValidationMessage.Visibility = Visibility.Visible;
                OkButton.IsEnabled = false;
            }
            else
            {
                ValidationMessage.Visibility = Visibility.Collapsed;
                OkButton.IsEnabled = true;
            }
        }

        private static bool TryParseBox(TextBox box, int min, int max, out int value)
        {
            if (int.TryParse(box.Text.Trim(), out value) && value >= min && value <= max)
                return true;
            value = 0;
            return false;
        }

        // ---- Slider <-> TextBox sync ----

        private void WaitSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_syncingSlider && WaitBox != null)
            {
                _syncingSlider = true;
                WaitBox.Text = ((int)WaitSlider.Value).ToString();
                _syncingSlider = false;
            }
        }

        private void WaitBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_syncingSlider && int.TryParse(WaitBox.Text, out int v))
            {
                _syncingSlider = true;
                WaitSlider.Value = Math.Clamp(v, WaitSlider.Minimum, WaitSlider.Maximum);
                _syncingSlider = false;
            }
            ValidateForm(sender, e);
        }

        private void RepeatsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_syncingSlider && RepeatsBox != null)
            {
                _syncingSlider = true;
                RepeatsBox.Text = ((int)RepeatsSlider.Value).ToString();
                _syncingSlider = false;
            }
        }

        private void RepeatsBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_syncingSlider && int.TryParse(RepeatsBox.Text, out int v))
            {
                _syncingSlider = true;
                RepeatsSlider.Value = Math.Clamp(v, RepeatsSlider.Minimum, RepeatsSlider.Maximum);
                _syncingSlider = false;
            }
            ValidateForm(sender, e);
        }

        private void WorkSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_syncingSlider && WorkBox != null)
            {
                _syncingSlider = true;
                WorkBox.Text = ((int)WorkSlider.Value).ToString();
                _syncingSlider = false;
            }
        }

        private void WorkBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_syncingSlider && int.TryParse(WorkBox.Text, out int v))
            {
                _syncingSlider = true;
                WorkSlider.Value = Math.Clamp(v, WorkSlider.Minimum, WorkSlider.Maximum);
                _syncingSlider = false;
            }
            ValidateForm(sender, e);
        }

        private void RestSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_syncingSlider && RestBox != null)
            {
                _syncingSlider = true;
                RestBox.Text = ((int)RestSlider.Value).ToString();
                _syncingSlider = false;
            }
        }

        private void RestBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_syncingSlider && int.TryParse(RestBox.Text, out int v))
            {
                _syncingSlider = true;
                RestSlider.Value = Math.Clamp(v, RestSlider.Minimum, RestSlider.Maximum);
                _syncingSlider = false;
            }
            ValidateForm(sender, e);
        }

        private void NumberOnly_PreviewInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^\d+$");
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            TryParseBox(WaitBox, 0, 3600, out int wait);
            TryParseBox(RepeatsBox, 1, 999, out int repeats);
            TryParseBox(WorkBox, 1, 3600, out int work);
            TryParseBox(RestBox, 1, 3600, out int rest);

            ResultSequence = new TabataSequence
            {
                Id = _original?.Id ?? Guid.NewGuid(),
                Name = NameBox.Text.Trim(),
                WaitSeconds = wait,
                Repeats = repeats,
                WorkSeconds = work,
                RestSeconds = rest
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
