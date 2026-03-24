using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TabataTimer.Models;
using TabataTimer.Services;

namespace TabataTimer
{
    public partial class MainWindow : Window
    {
        private AppSettings _settings;

        public MainWindow()
        {
            InitializeComponent();
            _settings = SettingsManager.Load();
            RefreshList();

            Loaded += (s, e) => ApplyLayout(_settings.MainWindowLayout);
            Closing += (s, e) =>
            {
                _settings.MainWindowLayout = CaptureLayout();
                SettingsManager.Save(_settings);
            };
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

        private WindowLayout CaptureLayout() => new()
        {
            Left = Left,
            Top = Top,
            Width = Width,
            Height = Height
        };

        private void RefreshList()
        {
            SequenceListPanel.Children.Clear();

            if (_settings.Sequences.Count == 0)
            {
                EmptyStatePanel.Visibility = Visibility.Visible;
                StatusText.Text = "No sequences — create one to get started";
                return;
            }

            EmptyStatePanel.Visibility = Visibility.Collapsed;
            StatusText.Text = $"{_settings.Sequences.Count} sequence{(_settings.Sequences.Count == 1 ? "" : "s")} saved";

            foreach (var seq in _settings.Sequences)
            {
                SequenceListPanel.Children.Add(BuildSequenceCard(seq));
            }
        }

        private UIElement BuildSequenceCard(TabataSequence seq)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(20, 16, 20, 16)
            };

            var outer = new Grid();
            outer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            outer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            outer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Left: name + stats
            var leftPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            var nameBlock = new TextBlock
            {
                Text = seq.Name,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5)),
                FontFamily = new FontFamily("Segoe UI Semibold"),
                Margin = new Thickness(0, 0, 0, 8)
            };
            leftPanel.Children.Add(nameBlock);

            // Stats row
            var statsPanel = new StackPanel { Orientation = Orientation.Horizontal };
            statsPanel.Children.Add(BuildStat("WAIT", seq.WaitDisplay, "#A0A0A0"));
            statsPanel.Children.Add(BuildStatDivider());
            statsPanel.Children.Add(BuildStat("REPEAT", seq.Repeats.ToString() + "×", "#3B82F6"));
            statsPanel.Children.Add(BuildStatDivider());
            statsPanel.Children.Add(BuildStat("WORK", seq.WorkDisplay, "#22C55E"));
            statsPanel.Children.Add(BuildStatDivider());
            statsPanel.Children.Add(BuildStat("REST", seq.RestDisplay, "#FF4500"));
            leftPanel.Children.Add(statsPanel);

            Grid.SetColumn(leftPanel, 0);
            outer.Children.Add(leftPanel);

            // Move Up / Down buttons (center column)
            var movePanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0)
            };

            var moveUpBtn = CreateMoveButton("▲", seq);
            moveUpBtn.Click += (s, e) => MoveSequence(seq, -1);

            var moveDownBtn = CreateMoveButton("▼", seq);
            moveDownBtn.Click += (s, e) => MoveSequence(seq, 1);

            movePanel.Children.Add(moveUpBtn);
            movePanel.Children.Add(moveDownBtn);
            Grid.SetColumn(movePanel, 1);
            outer.Children.Add(movePanel);

            // Right: action buttons
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 0)
            };

            var startBtn = CreateButton("▶  START", "#22C55E", "#16A34A", 14);
            startBtn.Padding = new Thickness(18, 10, 18, 10);
            startBtn.Click += (s, e) => StartSequence(seq);

            var editBtn = CreateButton("✏  EDIT", "#3B82F6", "#2563EB", 13);
            editBtn.Margin = new Thickness(8, 0, 0, 0);
            editBtn.Click += (s, e) => EditSequence(seq);

            var copyBtn = CreateButton("⎘  COPY", "#0EA5E9", "#0284C7", 13);
            copyBtn.Margin = new Thickness(8, 0, 0, 0);
            copyBtn.Click += (s, e) => DuplicateSequence(seq);

            var deleteBtn = CreateButton("🗑  DELETE", "#EF4444", "#DC2626", 13);
            deleteBtn.Margin = new Thickness(8, 0, 0, 0);
            deleteBtn.Click += (s, e) => DeleteSequence(seq);

            btnPanel.Children.Add(startBtn);
            btnPanel.Children.Add(editBtn);
            btnPanel.Children.Add(copyBtn);
            btnPanel.Children.Add(deleteBtn);

            Grid.SetColumn(btnPanel, 2);
            outer.Children.Add(btnPanel);

            card.Child = outer;
            return card;
        }

        private Button CreateMoveButton(string symbol, TabataSequence seq)
        {
            return new Button
            {
                Content = symbol,
                Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0)),
                FontSize = 11,
                Padding = new Thickness(8, 3, 8, 3),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(0, 0, 0, 2)
            };
        }

        private void MoveSequence(TabataSequence seq, int direction)
        {
            var idx = _settings.Sequences.FindIndex(s => s.Id == seq.Id);
            var newIdx = idx + direction;
            if (newIdx < 0 || newIdx >= _settings.Sequences.Count) return;

            _settings.Sequences.RemoveAt(idx);
            _settings.Sequences.Insert(newIdx, seq);
            SettingsManager.Save(_settings);
            RefreshList();
        }

        private static UIElement BuildStat(string label, string value, string colorHex)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 0) };
            panel.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A0A0A0")),
                Margin = new Thickness(0, 0, 0, 2)
            });
            panel.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)),
                FontFamily = new FontFamily("Consolas")
            });
            return panel;
        }

        private static UIElement BuildStatDivider()
        {
            return new Border
            {
                Width = 1,
                Background = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
                Margin = new Thickness(16, 0, 16, 0)
            };
        }

        private static Button CreateButton(string text, string bgColor, string hoverColor, double fontSize)
        {
            var btn = new Button
            {
                Content = text,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgColor)),
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = fontSize,
                FontFamily = new FontFamily("Segoe UI Semibold"),
                Padding = new Thickness(14, 8, 14, 8),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            btn.Template = CreateRoundedButtonTemplate();

            btn.MouseEnter += (s, e) =>
                btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hoverColor));
            btn.MouseLeave += (s, e) =>
                btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgColor));

            return btn;
        }

        private static ControlTemplate CreateRoundedButtonTemplate()
        {
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetBinding(Border.BackgroundProperty,
                new System.Windows.Data.Binding { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent), Path = new PropertyPath("Background") });
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            border.SetBinding(Border.PaddingProperty,
                new System.Windows.Data.Binding { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent), Path = new PropertyPath("Padding") });

            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(content);
            template.VisualTree = border;
            return template;
        }

        private void NewSequence_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new EditSequenceWindow(null, _settings, _settings.Sequences);
            dlg.Owner = this;
            if (dlg.ShowDialog() == true && dlg.ResultSequence != null)
            {
                _settings.Sequences.Add(dlg.ResultSequence);
                SettingsManager.Save(_settings);
                RefreshList();
            }
        }

        private void EditSequence(TabataSequence seq)
        {
            var dlg = new EditSequenceWindow(seq, _settings, _settings.Sequences);
            dlg.Owner = this;
            if (dlg.ShowDialog() == true && dlg.ResultSequence != null)
            {
                var idx = _settings.Sequences.FindIndex(s => s.Id == seq.Id);
                if (idx >= 0)
                    _settings.Sequences[idx] = dlg.ResultSequence;
                SettingsManager.Save(_settings);
                RefreshList();
            }
        }

        private void DuplicateSequence(TabataSequence seq)
        {
            // Generate unique name: "Copy of X" → "Copy of X (2)" → ...
            string baseName = "Copy of " + seq.Name;
            var existingNames = _settings.Sequences
                .Select(s => s.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            string newName = baseName;
            for (int n = 2; existingNames.Contains(newName); n++)
                newName = $"{baseName} ({n})";

            var clone = new TabataSequence
            {
                Id = Guid.NewGuid(),
                Name = newName,
                WaitSeconds = seq.WaitSeconds,
                Repeats = seq.Repeats,
                WorkSeconds = seq.WorkSeconds,
                RestSeconds = seq.RestSeconds,
                CallOutMode = seq.CallOutMode,
                CallOutList = new List<string>(seq.CallOutList),
                VoiceName = seq.VoiceName
            };

            _settings.Sequences.Add(clone);
            SettingsManager.Save(_settings);
            RefreshList();
        }

        private void DeleteSequence(TabataSequence seq)
        {
            var result = MessageBox.Show(
                $"Delete sequence \"{seq.Name}\"?\n\nThis cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _settings.Sequences.RemoveAll(s => s.Id == seq.Id);
                SettingsManager.Save(_settings);
                RefreshList();
            }
        }

        private void StartSequence(TabataSequence seq)
        {
            var timerWin = new TimerWindow(seq, _settings);
            timerWin.Owner = this;
            timerWin.SettingsChanged += (s, e) => SettingsManager.Save(_settings);
            timerWin.ShowDialog();
        }
    }
}
