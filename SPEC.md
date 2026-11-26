# RoboUI – Spec for Claude Code

## Goal
A Windows 11 desktop app (WPF, .NET 10, WPF-UI library) that wraps robocopy.exe so a user can run
large copy jobs, stop them anytime, shut down the PC, and resume the next day. Robocopy does all the
copying. The app builds commands, runs robocopy, reports progress, and keeps saved jobs.

## Current state
- Project created with `dotnet new wpf`, package `WPF-UI` added, App.xaml uses ThemesDictionary +
  ControlsDictionary, MainWindow is a `ui:FluentWindow` with Mica.
- Inspect the project before changing anything. Do not recreate it.

## Hard rules
- Target `net10.0-windows`, C# latest, nullable enabled, warnings treated as errors in the core project.
- MVVM with `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`). No logic in code-behind
  except pure view concerns.
- Never block the UI thread. Use async/await, `Progress<T>` / dispatcher for UI updates.
- Do not add features beyond this spec. If something is ambiguous, pick the simplest option and note it
  in the milestone summary.
- After each milestone: `dotnet build` and `dotnet test` must pass with zero warnings. Summarize what
  changed and what to test by hand. Stop and wait for review before the next milestone.
- Never pass /MIR or /PURGE unless the user explicitly enabled Mirror for that job AND confirmed a
  warning dialog that names the destination path.

## Solution layout
- `RoboUI.Core` (class library, no WPF references): models, command builder, output parser,
  runner, exit-code interpretation, job store.
- `RoboUI` (WPF app): views, view models, services wiring (DI via Microsoft.Extensions.Hosting).
- `RoboUI.Core.Tests` (xUnit): unit tests for everything in Core that does not start a process.

## Robocopy facts the implementation must respect
- Path quoting: a quoted path ending in `\` breaks argument parsing (`"D:\"` escapes the quote).
  Trim trailing backslashes; pass drive roots as `D:\` unquoted; quote only paths containing spaces.
- Always add for machine-readable output: `/BYTES /FP /NDL /NP /NJH`. `/NP` is mandatory (percentage
  output uses carriage returns). For the scan pass add `/L /NJS`.
- File lines look like: `\t    New File  \t\t   123456\tC:\full\path.ext`. The class text may be localized;
  parse size and path structurally (tabs + digits + path starting with `X:\` or `\\`). Lines whose
  class starts with `*` (`*EXTRA File`) are not copies; ignore them for progress.
- Error lines contain `(0x` + 8 hex digits + `)`; the next non-empty line is the message. Also detect
  `RETRY LIMIT EXCEEDED`.
- Console output encoding is the OEM code page
  (`CultureInfo.CurrentCulture.TextInfo.OEMCodePage`); register `CodePagesEncodingProvider`.
- Log with `/UNILOG+:<path> /TEE` so the log is Unicode and stdout still streams.
- Exit codes are bit flags: 0 nothing to do, 1 copied, 2 extras, 4 mismatches, 8 failures (some files
  not copied), 16 fatal. >= 8 is failure. Map to a typed result with a human message.
- Stopping = kill the robocopy process. Resume = run the same command again; robocopy skips identical
  files and recopies partial ones (and resumes them with /Z). Track "stopped by user" separately from
  the exit code.
- Validation before start: source exists; destination not equal to or inside source; threads 1-128.

## Features by milestone

### Milestone 1 – Core engine + tests
- `CopyJob` model: Id, Name, Source, Destination, IncludeSubfolders (/E), Restartable (/Z),
  SkipOlder (/XO), Mirror (/MIR), Threads (/MT, default 8), Retries (/R, default 2),
  WaitSeconds (/W, default 5), ExcludeFiles, ExcludeDirs, PreScan (default true), WriteLog (default true),
  KeepAwake (default true).
- `RobocopyCommandBuilder`: job -> argument string, for both scan and copy passes. Unit-tested
  (quoting, roots, UNC, spaces, excludes, mirror overriding /E).
- `RobocopyOutputParser`: line -> FileEvent | ErrorEvent | Ignore. Unit-tested with sample lines,
  including extras, UNC paths, retries.
- `RobocopyRunner`: runs a pass, streams parsed events via IAsyncEnumerable or IProgress, supports
  CancellationToken (kills process), returns exit code. Scan pass totals files/bytes.
- `ExitCodeInterpreter`: unit-tested for every flag combination that matters.

### Milestone 2 – Main window (single job)
- Fluent UI: source/destination with folder pickers (`Microsoft.Win32.OpenFolderDialog`) and folder
  drag-drop; options grouped in cards; live read-only command preview with Copy button.
- Start/Resume and Stop buttons; progress card: phase (Scanning/Copying), percent, files done/total,
  bytes done/total, speed (20-second moving window), ETA, current file, elapsed.
- Errors panel (last 500 lines). Status colored by result (success, stopped, failures).
- Progress must not run ahead: credit a file's bytes when the next file starts or the pass ends.
  Throttle UI updates to ~4/sec.
- Keep PC awake during a run via `SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED)`; reset
  on finish/stop/exit.
- Closing the window during a run: confirm, stop cleanly, then exit.

### Milestone 3 – Jobs persistence
- Jobs stored as JSON in `%APPDATA%\RoboUI\jobs.json` (System.Text.Json). Left navigation lists
  jobs; add, duplicate, rename, delete (with confirm). Last selected job reopens on startup.
- Per job: last run time, last result, last log path. "Open log" and "Open destination" buttons.
- Logs in `%APPDATA%\RoboUI\logs\<job>_<yyyyMMdd_HHmmss>.log`; keep the newest 50 per job.

### Milestone 4 – Windows integration
- Taskbar progress (`TaskbarItemInfo`), including paused/error states.
- Tray icon with show/hide and current progress tooltip; minimize to tray while running (setting).
- Toast notification when a job finishes (success, failures, stopped).
- Optional after-completion action per run: nothing / sleep / hibernate / shut down, with a 60-second
  cancellable countdown dialog.
- Follow system light/dark theme and accent color.

### Milestone 5 – Polish and ship
- Settings page: theme, minimize to tray, default thread count, log retention.
- App icon, About page with version.
- Publish profile: single-file, self-contained, win-x64, `IncludeNativeLibrariesForSelfExtract`.
- README.md with usage, screenshots placeholders, and the resume behavior explained.

## Out of scope (do not build)
- Own copy engine, network credential management, cloud targets, scheduling UI (may come later),
  installer/MSIX, auto-update, telemetry.

## Definition of done per milestone
- Builds and tests pass with zero warnings.
- No UI freeze with a 100k-file job (verify by reasoning about threading; note any risk).
- Summary: files changed, design decisions, manual test checklist.