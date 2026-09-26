# Speedcubing Trainer

A cross-platform app to help you improve at the 3x3 Rubik's cube: time and track solves, generate WCA-style random-state scrambles, and learn, drill and memorize F2L / OLL / PLL algorithms.

Built with [Uno Platform](https://platform.uno) (C# / WinUI XAML) and runs on Android, iOS, the browser (WebAssembly) and desktop (Windows, macOS, Linux) from a single code base. All data stays on your device.

## Features

- **Timer**: hold space (desktop) or press and hold the timer area (touch), release to start, any key or tap to stop. WCA inspection with automatic +2 (over 15 s) and DNF (over 17 s), +2 / DNF toggles and delete for the last solve, scramble text with a cube-net preview.
- **Scrambles**: random-state scrambles from a built-in Kociemba two-phase solver written from scratch in C#. The solver tables are generated on first launch (about a second on desktop, longer in the browser) and cached, and random-move scrambles are used until they are ready.
- **Sessions & statistics**: multiple sessions; single, mo3, ao5, ao12, ao50 and ao100 with WCA trimming and DNF handling; current and best values; a trend chart and a time histogram; a solve list with per-solve penalty and comment editing; JSON export/import of everything and CSV export per session.
- **Algorithm library**: all 41 F2L, 57 OLL and 21 PLL cases. Every case picture is computed from the algorithm itself (the case is the inverse of the algorithm applied to a solved cube), so pictures and algorithms can never disagree. Alternate algorithms, learning status (not started / learning / learned), favorites, setup scrambles to reproduce a case on a real cube, plus the mirror and inverse of any algorithm.
- **Trainer**
  - *Drill*: a random case from your chosen pool with a setup scramble; time recognition and execution separately with the space bar or a tap; per-case attempt history, best and mean times.
  - *Memorize*: spaced repetition (SM-2) over the cases you are learning, with Again / Hard / Good / Easy grading and interval previews, plus a configurable number of new cases per day.
  - *Recognition quiz*: pick the case name from four choices.
- **Settings**: inspection, hold-to-start delay, timer size, scramble type, wide-move notation (`r` vs `Rw`), delete confirmation, new cases per day, light/dark theme.

## Project layout

| Path | Purpose |
|---|---|
| `SpeedcubingTrainer/` | Uno Platform single-project app (`net10.0-android`, `net10.0-ios`, `net10.0-browserwasm`, `net10.0-desktop`): views, view models, controls, platform services. |
| `SpeedcubingTrainer.Core/` | UI-independent library: cube model and notation parser, scramblers, two-phase solver, algorithm sets, WCA statistics, spaced repetition, JSON persistence. |
| `SpeedcubingTrainer.Core.Tests/` | xUnit tests for the core library, including validation of every algorithm in the data files against the cube model. |

## Building and running

Prerequisites: .NET SDK 10 (see `global.json`) and the Uno Platform workloads for the heads you want to run. `dotnet workload install wasm-tools` is enough for the browser; see [Uno's setup guide](https://platform.uno/docs/articles/get-started.html) for mobile.

```bash
# Core tests
dotnet test SpeedcubingTrainer.Core.Tests

# Desktop (Windows, macOS, Linux)
dotnet run --project SpeedcubingTrainer -f net10.0-desktop

# Browser (WebAssembly), served at http://localhost:5000
dotnet run --project SpeedcubingTrainer -f net10.0-browserwasm

# Android (device or emulator attached)
dotnet build SpeedcubingTrainer -f net10.0-android -t:Run
```

The solution file is `SpeedcubingTrainer.slnx`; open it in Visual Studio 2022 17.13+ or Rider.

## Continuous integration and deployment

- `.github/workflows/ci.yml` runs the core tests and builds the desktop and WebAssembly heads on every push and pull request.
- `.github/workflows/deploy-pages.yml` publishes the WebAssembly head (AOT compiled) to GitHub Pages on every push to `main`. Enable GitHub Pages with the "GitHub Actions" source in the repository settings to use it.

## Notes on the cube engine

- Facelet permutations are derived from the cube's geometry, not typed in by hand; the piece-level (cubie) model is built from the same geometry and the two are cross-checked in tests.
- The two-phase solver uses coordinate move tables and breadth-first pruning tables (about 5.6 MB) without symmetry reduction. It targets solutions of 21 moves or fewer and accepts up to 24, which is what random-state scrambles need.
- Algorithm data lives in `SpeedcubingTrainer.Core/Algorithms/Data/*.json`. Tests check that every OLL algorithm orients the last layer, every PLL algorithm solves the cube up to AUF, every F2L algorithm solves the pair without disturbing other slots, that cases are distinct, and that the F2L set covers all 41 cases. Case numbering follows the common speedsolving lists where possible; a handful of F2L cases are named descriptively.
