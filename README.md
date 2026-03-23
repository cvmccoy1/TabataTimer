# Tabata Timer

A standalone WPF desktop application for Tabata interval training.

---

## Requirements

- **Windows 10/11** (64-bit)
- **.NET 8 SDK** — download from https://dotnet.microsoft.com/download/dotnet/8.0

---

## Building

### Option A: Double-click build script
Run `build.bat` in the project root. The executable will appear in `.\publish\TabataTimer.exe`.

### Option B: Manual command line
```cmd
cd TabataTimer
dotnet publish TabataTimer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ..\publish
```

---

## Features

### Main Screen
- Lists all saved sequences with Wait / Repeat / Work / Rest stats
- **Start**, **Edit**, and **Delete** buttons per sequence
- **+ NEW SEQUENCE** to add a new one

### Sequence Editor
- Set: Name, Wait-to-Start, Repeats, Work Time, Rest Time
- Sliders + text boxes (both synced)
- Duplicate name detection and validation
- OK / Cancel buttons

### Timer Screen
- Large countdown display with color coding:
  - **Gray** = Wait phase
  - **Green** = Work phase
  - **Orange-Red** = Rest phase
  - **Yellow** = Done
- Count-up total time display
- Round counter (e.g., "3 of 8")
- **Start / Pause / Stop (reset) / Exit** controls
- **Volume slider** — controls beep volume
- **Warning Countdown Beep** checkbox
  - If checked: beeps at 3, 2, 1 seconds before end of each phase
  - End-of-phase: double beep
  - Workout complete: ascending 3-tone fanfare

### Persistence
- All sequences and settings (volume, warning beep toggle) persist across restarts
- Stored in `%APPDATA%\TabataTimer\settings.json`

---

## Beep Reference

| Event | Sound |
|-------|-------|
| Warning countdown (3, 2, 1) | Short single tick (880 Hz, 80ms) |
| End of phase (Work → Rest, Rest → Work, Wait → Work) | Double beep (660 Hz × 2) |
| Workout fully complete | Ascending C-E-G fanfare |

---

## Project Structure

```
TabataTimer/
├── build.bat                          ← Build script
├── README.md
└── TabataTimer/
    ├── TabataTimer.csproj
    ├── App.xaml / App.xaml.cs         ← Application entry + global styles
    ├── MainWindow.xaml / .cs          ← Sequence list
    ├── EditSequenceWindow.xaml / .cs  ← Create/edit sequence
    ├── TimerWindow.xaml / .cs         ← Active timer
    ├── Models/
    │   └── TabataSequence.cs          ← Data models + AppSettings
    └── Services/
        ├── SettingsManager.cs         ← JSON persistence
        └── AudioService.cs            ← Beep sounds
```
