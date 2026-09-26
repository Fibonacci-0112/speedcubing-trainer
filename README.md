# Speedcubing Trainer

A cross-platform app to help you improve at the 3x3 Rubik's cube: time and track solves, generate WCA-style random-state scrambles, and learn, drill and memorize F2L / OLL / PLL algorithms.

Built with [Uno Platform](https://platform.uno) (C# / WinUI XAML) and runs on Android, iOS, the browser (WebAssembly) and desktop (Windows, macOS, Linux) from a single code base. All data stays on your device.

## Features

- **Timer** with WCA inspection (+2 at 15 s, DNF at 17 s), hold-to-start on keyboard or touch, +2 / DNF penalties, scramble preview.
- **Scrambles**: random-state 3x3 scrambles from a built-in two-phase solver (random-move scrambles are used until the solver tables are ready).
- **Sessions & statistics**: multiple sessions, single / mo3 / ao5 / ao12 / ao50 / ao100 / mean with WCA trimming, personal bests, trend and distribution charts, JSON import/export and CSV export.
- **Algorithm library**: F2L, OLL and PLL with case images computed from the algorithms themselves, alternate algs, learning status and favorites, and setup scrambles to reproduce any case on a real cube.
- **Trainer**: drill mode with per-case timing, spaced-repetition memorization (SM-2), and a recognition quiz.

## Project layout

| Path | Purpose |
|---|---|
| `SpeedcubingTrainer/` | Uno Platform single-project app (`net10.0-android`, `net10.0-ios`, `net10.0-browserwasm`, `net10.0-desktop`). |
| `SpeedcubingTrainer.Core/` | UI-independent library: cube model and notation, scramblers, two-phase solver, algorithm sets, statistics, spaced repetition, persistence. |
| `SpeedcubingTrainer.Core.Tests/` | xUnit tests for the core library. |

## Building and running

Prerequisites: .NET SDK 10 (see `global.json`) and the Uno Platform workloads for the heads you want to run. `dotnet workload install wasm-tools` is enough for the browser; see [Uno's setup guide](https://platform.uno/docs/articles/get-started.html) for mobile.

```bash
# Core tests
dotnet test SpeedcubingTrainer.Core.Tests

# Desktop (Windows, macOS, Linux)
dotnet run --project SpeedcubingTrainer -f net10.0-desktop

# Browser (WebAssembly)
dotnet run --project SpeedcubingTrainer -f net10.0-browserwasm

# Android (device or emulator attached)
dotnet build SpeedcubingTrainer -f net10.0-android -t:Run
```

The solution file is `SpeedcubingTrainer.slnx`; open it in Visual Studio 2022 17.13+ or Rider.
