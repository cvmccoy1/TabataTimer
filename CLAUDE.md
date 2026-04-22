# TabataTimer - WPF Interval Training Timer

## Tech Stack
- WPF (Windows Presentation Foundation) with .NET 8
- MVVM pattern using CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`)
- XAML for UI, C# for code-behind
- Services: `AudioService`, `TtsService`, `CallOutEngine`

## Key Conventions

### MVVM Pattern
- ViewModels are in `TabataTimer/ViewModels/`
- Views are in `TabataTimer/Views/` with `.xaml` and `.xaml.cs`
- Models are in `TabataTimer/Models/`
- Services are in `TabataTimer/Services/` (interfaces in `Services/Interfaces/`)
- Converters are in `TabataTimer/Converters/`

### Window Layout Persistence
- All windows save/restore position and size via `WindowLayout` model
- Layout is saved in `AppSettings` (e.g., `MainWindowLayout`, `EditDialogLayouts`, `TimerWindowLayout`)
- Use `Loaded` event to apply layout, `Closing` event to capture layout
- Width/Height use `MinWidth`/`MinHeight` constraints

### Commands and Events
- ViewModels expose `ICommand` properties for button actions via `[RelayCommand]`
- Window-to-viewmodel communication: ViewModels raise events (e.g., `OkRequested`, `CancelRequested`, `ExitRequested`)
- Windows listen to events and call `Close()` or set `DialogResult`
- Drag-drop events are wired in code-behind (`PreviewMouseLeftButtonDown` / `MouseMove` for initiation; `DragEnter`/`Drop` on targets); the ViewModel exposes public methods that the code-behind calls directly:
  - `MoveItemToFolder` / `MoveItemToBreadcrumb` — move a card into a folder or up to a breadcrumb level
  - `ReorderItem` — reposition a folder or sequence within the current level (drop between cards)
  - `MoveCallOutItem` — reorder a Call Out list entry by drag-drop in the Edit Sequence window

### Folder Hierarchy and Navigation
- Sequences can be organised into nested `SequenceFolder`s stored in `AppSettings.RootFolders`
- Root-level sequences remain in `AppSettings.Sequences` (backward-compatible)
- `MainWindowViewModel` holds a `_folderPath` list; empty = root, last item = current folder
- `CurrentItems` (`ObservableCollection<object>`) is the unified display list (folders first, then sequences)
- `Breadcrumbs` (`ObservableCollection<BreadcrumbItem>`) reflects the navigation path
- Breadcrumb items and folder cards both accept drag-drop targets
- Folders are rendered via an implicit `DataTemplate` for `FolderViewModel`; sequences via an implicit `DataTemplate` for `TabataSequenceViewModel` — no explicit `ItemTemplate` on the `ItemsControl`
- In-place reordering: `ItemsGrid` (a named `Grid` wrapping the `ScrollViewer`) is the drop target; an `InsertionIndicator` `Border` overlay is positioned via `panel.TranslatePoint(..., ItemsGrid)` for scroll-aware Y coordinates; folders clamp to 0..folderCount, sequences clamp to folderCount..folderCount+seqCount

### Settings
- Global settings stored in `AppSettings` via `SettingsManager` (JSON at `%APPDATA%\TabataTimer\settings.json`)
- Sequence-specific settings: `Volume` and `WarningBeepEnabled` are per-sequence (not global)
- Folder structure is stored as nested `SequenceFolder` objects inside `AppSettings.RootFolders`

### Common UI Patterns
- Modern dark styling uses `StaticResource` references from `App.xaml` for consistency
- Available button styles: `AccentButton` (orange-red), `GreenButton`, `BlueButton`, `YellowButton`, `RedButton`, `CyanButton`, `GhostButton`
- Binding patterns: `Mode=TwoWay` with `UpdateSourceTrigger=PropertyChanged` for real-time updates
- `BoolToVisibilityConverter` for conditional visibility (with `ConverterParameter=Invert` for negation)

## Notes
- CallOut exercises support comma-separated multiple exercises per slot; when a slot has multiple options, `CallOutEngine.PickFromSlot` picks **randomly** using a global `_usedExercises` `HashSet<string>` shared across all slots — an option is excluded until all options in that slot have been used, then that slot's options are cleared from the set and the cycle restarts
- TTS service uses Windows Speech API (WinRT `SpeechSynthesizer`) for exercise name announcement
- Timer runs on background thread, UI updates via `Dispatcher`
- `Repeats` and `CallOutItems` are independent in Follow and Random modes (only Repeat mode constrains minimum entries)
- Drag-drop does not allow dropping a folder into itself or into one of its own descendants (ancestor check in `IsAncestorOf`)
- `FolderNameWindow` is a simple dialog reused for both "New Folder" and "Rename Folder"; the confirm button reads "Save Folder" or "Update Folder" accordingly
- Call Out list rows show a `≡` drag handle (column 0); drags originating from a `TextBox` source are suppressed via `FindParent<TextBox>` so text editing is unaffected; `VirtualizingStackPanel.IsVirtualizing="False"` is required on the `ListBox` for `TranslatePoint`-based coordinate calculations to work correctly
