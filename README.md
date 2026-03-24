# Tabata Timer

A standalone WPF desktop application for Tabata and custom interval training. Create, manage, and run any number of named interval sequences, each with configurable wait, repeat, work, and rest times.

---

## Requirements

| Requirement | Details |
|---|---|
| OS | Windows 10 or Windows 11 (64-bit) |
| IDE | Visual Studio 2022 (for building from source) |
| SDK | .NET 8 SDK — included with VS 2022, or download from https://dotnet.microsoft.com/download/dotnet/8.0 |

---

## Building

### Option A — Visual Studio 2022
1. Open `TabataTimer\TabataTimer.csproj` in Visual Studio 2022
2. NuGet packages restore automatically on first open
3. Press **F5** to run, or **Build → Publish** to produce a standalone executable

### Option B — Command line (build.bat)
Run `build.bat` from the project root. The self-contained single-file executable is written to `.\publish\TabataTimer.exe`.

```cmd
cd C:\git\TabataTimer
build.bat
```

---

## Features

### Main Screen
- Lists all saved sequences with **Wait / Repeat / Work / Rest** values visible per row
- **+ NEW SEQUENCE** button to create a new sequence
- Per-row buttons: **Start**, **Edit**, **Delete**
- Delete prompts for confirmation before removing

### Sequence Editor (New and Edit)
- Fields: **Name**, **Wait to Start**, **Repeats**, **Work Time**, **Rest Time**
- Each time field has a linked **slider + text box** — editing either one updates the other
- Sliders support click-to-position (clicking anywhere on the track jumps the thumb to that point)
- **Validation** prevents saving when:
  - Name is blank
  - A sequence with the same name already exists (case-insensitive)
  - Any numeric field is outside its allowed range
- Validation errors shown inline; the Save button is disabled until all fields pass
- **Cancel** discards changes; **Save / Update** commits

#### Field Limits

| Field | Minimum | Maximum |
|---|---|---|
| Wait to Start | 0 sec | 120 sec |
| Repeats | 1 | 100 |
| Work Time | 1 sec | 300 sec |
| Rest Time | 1 sec | 300 sec |

All limits are defined once in `Constants.cs` (`TimerConstraints` class). Changing a value there automatically updates the sliders, validation logic, and error messages everywhere in the app.

### Timer Screen
Launched via the **Start** button on a sequence row.

#### Display Elements

| Element | Description |
|---|---|
| Sequence Name | Displayed at the top |
| Phase Label | Current phase — READY, WAIT, WORK, REST, or DONE — color-coded |
| Countdown Timer | Large MM:SS display counting down the current phase |
| Total Time | MM:SS count-up of total elapsed workout time, starts at 00:00 |
| Round | Current repeat, e.g. `3 of 8`; shows `0 of N` before the first Work phase begins |

#### Phase Colors

| Phase | Color |
|---|---|
| Ready (idle) | White |
| Wait | Gray |
| Work | Green |
| Rest | Orange-Red |
| Done | Yellow |

#### Timer Flow

```
START -> [Wait] -> Work -> Rest -> Work -> Rest -> ... (N repeats) -> DONE
```

The Wait phase is skipped when Wait to Start is 0. The Round counter increments at the start of each Work phase. After the final Work phase completes, the timer stops and shows DONE.

#### Controls

| Button | Behavior |
|---|---|
| Start | Begins the sequence from the beginning |
| Pause | Freezes the timer; label changes to Resume to continue |
| Stop | Stops and fully resets to the initial state |
| Exit | Stops the timer and closes the Timer window |

#### Audio

| Event | Sound |
|---|---|
| Warning countdown (3, 2, 1 sec before end of each phase) | Short single high tick (880 Hz, 90 ms) |
| End of phase transition | Double beep (660 Hz x2) |
| Workout fully complete | Ascending three-tone fanfare (C5, E5, G5) |

Tones are synthesized in memory as PCM WAV data and played via WPF `MediaPlayer`, which correctly routes audio to the Windows default output device including HDMI and Bluetooth.

#### Volume and Warning Beep

- **Volume slider** — adjusts the level of all sounds
- **Warning Countdown Beep checkbox** — when checked, a warning tick plays at 3, 2, and 1 seconds before the end of every phase including Wait. The end-of-phase beep and final fanfare always play regardless of this setting.

---

## Settings Persistence

All settings are saved and restored automatically between sessions. No manual save step is required.

| Setting | Persisted |
|---|---|
| All sequences (name, wait, repeats, work, rest) | Yes |
| Volume level | Yes |
| Warning Beep on/off | Yes |

Storage location: `%APPDATA%\TabataTimer\settings.json`

---

## Project Structure

```
TabataTimer/
├── build.bat                            <- Command-line build and publish script
├── README.md                            <- This file
└── TabataTimer/
    ├── TabataTimer.csproj               <- Project file (.NET 8, WPF, win-x64, self-contained)
    ├── App.xaml / App.xaml.cs           <- Application entry point and global styles
    ├── Constants.cs                     <- All field min/max limits (single source of truth)
    ├── MainWindow.xaml / .cs            <- Sequence list (main screen)
    ├── EditSequenceWindow.xaml / .cs    <- Create and edit a sequence
    ├── TimerWindow.xaml / .cs           <- Active timer screen
    ├── tabata.ico                       <- Application icon
    ├── Models/
    │   └── TabataSequence.cs            <- TabataSequence and AppSettings data models
    └── Services/
        ├── AudioService.cs              <- PCM WAV tone synthesis and MediaPlayer playback
        └── SettingsManager.cs           <- JSON persistence to %APPDATA%
```

---

## Dependencies

| Package | Version | Purpose |
|---|---|---|
| Newtonsoft.Json | 13.0.3 | Serializing settings to and from JSON |

All other functionality uses the .NET 8 and WPF standard libraries. No other third-party packages are required.

---

## Developer Notes

**Changing field limits** — edit `TimerConstraints` in `Constants.cs` only. Slider bounds are set in the `EditSequenceWindow` constructor; validation messages are built dynamically from the same constants. No other files need to change.

**Audio routing** — `AudioService` uses WPF `MediaPlayer` on a dedicated STA background thread. `Console.Beep()` and `SoundPlayer` were intentionally avoided as they use legacy Windows audio APIs that ignore the current default output device and fail silently on HDMI, Bluetooth, and Remote Desktop sessions.

**Remote Desktop audio** — if running over RDP, ensure the session is configured with "Play on this computer" under Local Resources, and that the Windows Audio and Windows Audio Endpoint Builder services are running on the remote machine. Group Policy on domain-joined machines may also need "Allow audio and video playback redirection" set to Enabled or Not Configured.

**Publishing a single-file executable** — the project is pre-configured for self-contained single-file publish targeting win-x64. Use `build.bat` or run the following from the project folder:

```cmd
dotnet publish TabataTimer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ..\publish
```
