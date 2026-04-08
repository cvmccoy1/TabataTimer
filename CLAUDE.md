# TabataTimer - WPF Interval Training Timer

## Tech Stack
- WPF (Windows Presentation Foundation) with .NET
- MVVM pattern for architecture
- XAML for UI, C# for code-behind
- Services: `AudioService`, `TtsService`, `CallOutEngine`

## Key Conventions

### MVVM Pattern
- ViewModels are in `TabataTimer/ViewModels/`
- Views are in `TabataTimer/Views/` with `.xaml` and `.xaml.cs`
- Models are in `TabataTimer/Models/`
- Services are in `TabataTimer/Services/`
- Converters are in `TabataTimer/Converters/`

### Window Layout Persistence
- All windows save/restore position and size via `WindowLayout` model
- Layout is saved in `AppSettings` (e.g., `MainWindowLayout`, `EditSequenceWindowLayout`, `TimerWindowLayout`)
- Use `Loaded` event to apply layout, `Closing` event to capture layout
- Width/Height use `MinWidth`/`MinHeight` constraints

### Commands and Events
- ViewModels expose `ICommand` properties for button actions
- Window-to-viewmodel communication: ViewModels raise events (e.g., `OkRequested`, `CancelRequested`, `ExitRequested`)
- Windows listen to events and call `Close()` or set `DialogResult`

### Settings
- Global settings stored in `AppSettings` via `SettingsManager`
- Sequence-specific settings: `Volume` and `WarningBeepEnabled` are per-sequence (not global)

### Common UI Patterns
- Modern styling uses `StaticResource` references for consistency
- Binding patterns: `Mode=TwoWay` with `UpdateSourceTrigger=PropertyChanged` for real-time updates
- `BoolToVisibilityConverter` for conditional visibility (with `ConverterParameter=Invert` for negation)

## Notes
- CallOut exercises support comma-separated multiple exercises
- TTS service uses Windows Speech API for exercise name announcement
- Timer runs on background thread, UI updates via `Dispatcher`
- `Repeats` and `CallOutItems` are independent in Follow and Random modes (only Repeat mode constrains minimum entries)
