using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Shell;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoboUI.Core.Formatting;
using RoboUI.Core.History;
using RoboUI.Core.Logging;
using RoboUI.Core.Models;
using RoboUI.Core.Progress;
using RoboUI.Core.Robocopy;
using RoboUI.Core.Validation;
using RoboUI.Services;
using Wpf.Ui.Controls;

namespace RoboUI.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    public const int MaxErrorLines = 500;
    private static readonly TimeSpan UiInterval = TimeSpan.FromMilliseconds(250);

    private static readonly HashSet<string> JobProperties =
    [
        nameof(Source), nameof(Destination), nameof(IncludeSubfolders), nameof(Restartable),
        nameof(SkipOlder), nameof(Mode), nameof(Threads), nameof(Retries), nameof(WaitSeconds),
        nameof(ExcludeFiles), nameof(ExcludeDirs), nameof(PreScan), nameof(WriteLog), nameof(KeepAwake),
    ];

    private readonly RobocopyRunner _runner;
    private readonly IDialogService _dialogs;
    private readonly IKeepAwakeService _keepAwake;
    private readonly AppPaths _paths;
    private readonly RunHistoryService _history;
    private readonly Stopwatch _runClock = new();

    private readonly CopyJob _job = new();
    private CopyProgressTracker? _tracker;
    private CancellationTokenSource? _stopSource;
    private Task? _currentRun;
    private long _errorTotal;
    private string _finishedText = "Finished";
    private bool _isTestRun;

    public MainViewModel(RobocopyRunner runner, IDialogService dialogs, IKeepAwakeService keepAwake, AppPaths paths, RunHistoryService history)
    {
        _runner = runner;
        _dialogs = dialogs;
        _keepAwake = keepAwake;
        _paths = paths;
        _history = history;
        Errors.CollectionChanged += (_, _) => CopyErrorsCommand.NotifyCanExecuteChanged();
        LoadFrom(_job);
    }

    // ---- Job options -------------------------------------------------------------------------

    [ObservableProperty] public partial string Source { get; set; } = "";
    [ObservableProperty] public partial string Destination { get; set; } = "";
    [ObservableProperty] public partial bool IncludeSubfolders { get; set; }
    [ObservableProperty] public partial bool Restartable { get; set; }
    [ObservableProperty] public partial bool SkipOlder { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChooseSubfolders))]
    public partial CopyMode Mode { get; set; }

    /// <summary>Mirror always includes subfolders, so the checkbox is locked in that mode.</summary>
    public bool CanChooseSubfolders => Mode != CopyMode.Mirror;
    [ObservableProperty] public partial int? Threads { get; set; }
    [ObservableProperty] public partial int? Retries { get; set; }
    [ObservableProperty] public partial int? WaitSeconds { get; set; }

    /// <summary>Semicolon-separated file names or wildcards.</summary>
    [ObservableProperty] public partial string ExcludeFiles { get; set; } = "";

    /// <summary>Semicolon-separated folder names or paths.</summary>
    [ObservableProperty] public partial string ExcludeDirs { get; set; } = "";

    [ObservableProperty] public partial bool PreScan { get; set; }
    [ObservableProperty] public partial bool WriteLog { get; set; }
    [ObservableProperty] public partial bool KeepAwake { get; set; }

    [ObservableProperty] public partial string CommandPreview { get; set; } = "";

    // ---- Run state ---------------------------------------------------------------------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(TestRunCommand), nameof(StopCommand), nameof(BrowseSourceCommand), nameof(BrowseDestinationCommand))]
    public partial bool IsRunning { get; set; }

    public bool CanEdit => !IsRunning;

    [ObservableProperty] public partial string StartButtonText { get; set; } = "Start";

    [ObservableProperty] public partial bool IsStatusOpen { get; set; }
    [ObservableProperty] public partial string StatusTitle { get; set; } = "";
    [ObservableProperty] public partial string StatusMessage { get; set; } = "";
    [ObservableProperty] public partial InfoBarSeverity StatusSeverity { get; set; } = InfoBarSeverity.Informational;

    // ---- Progress (refreshed ~4 times a second while running) --------------------------------

    [ObservableProperty] public partial string PhaseText { get; set; } = "Idle";
    [ObservableProperty] public partial double ProgressPercent { get; set; }
    [ObservableProperty] public partial bool IsProgressIndeterminate { get; set; }
    [ObservableProperty] public partial string PercentText { get; set; } = "";
    [ObservableProperty] public partial string FilesText { get; set; } = "–";
    [ObservableProperty] public partial string BytesText { get; set; } = "–";
    [ObservableProperty] public partial string SpeedText { get; set; } = "–";
    [ObservableProperty] public partial string EtaText { get; set; } = "–";
    [ObservableProperty] public partial string ElapsedText { get; set; } = "–";
    [ObservableProperty] public partial string CurrentFile { get; set; } = "";

    public ObservableCollection<string> Errors { get; } = [];
    [ObservableProperty] public partial string ErrorsHeader { get; set; } = "Errors";

    // ---- Bottom bar --------------------------------------------------------------------------

    /// <summary>One-line status next to the buttons: live progress while running, then the outcome.</summary>
    [ObservableProperty] public partial string BarStatusText { get; set; } = "";
    [ObservableProperty] public partial InfoBarSeverity BarStatusSeverity { get; set; } = InfoBarSeverity.Informational;

    // ---- Taskbar button progress --------------------------------------------------------------
    // While running: green bar (red once a file has failed), or marching while the total is unknown.
    // After a run the final state stays on the taskbar button until the window is activated.
    [ObservableProperty] public partial TaskbarItemProgressState TaskbarState { get; set; } = TaskbarItemProgressState.None;
    [ObservableProperty] public partial double TaskbarValue { get; set; }

    /// <summary>Clears the finished run's taskbar progress once the user has seen the window.</summary>
    public void AcknowledgeTaskbarProgress()
    {
        if (!IsRunning) TaskbarState = TaskbarItemProgressState.None;
    }

    /// <summary>Log of the most recent run, if one was written.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLastLog))]
    [NotifyCanExecuteChangedFor(nameof(OpenLogCommand))]
    public partial string? LastLogPath { get; set; }

    public bool HasLastLog => LastLogPath is not null;

    // ---- Commands ----------------------------------------------------------------------------

    private bool CanStart() => !IsRunning;
    private bool CanStop() => IsRunning;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void BrowseSource()
    {
        if (_dialogs.PickFolder("Choose the source folder", Source) is { } folder) Source = folder;
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void BrowseDestination()
    {
        if (_dialogs.PickFolder("Choose the destination folder", Destination) is { } folder) Destination = folder;
    }

    [RelayCommand]
    private void CopyCommandPreview() => _dialogs.CopyToClipboard(CommandPreview);

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void SwapFolders() => (Source, Destination) = (Destination, Source);

    private bool CanOpenLog() => LastLogPath is not null;

    [RelayCommand(CanExecute = nameof(CanOpenLog))]
    private void OpenLog()
    {
        if (LastLogPath is { } path && !_dialogs.OpenInShell(path))
            ShowStatus(InfoBarSeverity.Error, "Cannot open the log", $"The file could not be opened:{Environment.NewLine}{path}");
    }

    private bool CanCopyErrors() => Errors.Count > 0;

    /// <summary>Copies the error lines shown in the list (the newest 500).</summary>
    [RelayCommand(CanExecute = nameof(CanCopyErrors))]
    private void CopyErrors() => _dialogs.CopyToClipboard(string.Join(Environment.NewLine, Errors));

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        if (ValidatedJob() is not { } job) return;

        // Mirror and Move delete files, so each needs its own confirmation.
        var deletionsConfirmed = false;
        if (job.Mode == CopyMode.Mirror)
        {
            deletionsConfirmed = await _dialogs.ConfirmAsync(
                "Mirror will delete files",
                "Mirror makes the destination an exact copy of the source.\n\n" +
                $"Files and folders in\n{job.Destination}\nthat are not in the source will be PERMANENTLY DELETED.\n\n" +
                "Tip: use Test run first to see what would be deleted.\n\nDo you want to continue?",
                confirmText: "Mirror and delete",
                destructive: true);
            if (!deletionsConfirmed) return;
        }
        else if (job.Mode == CopyMode.Move)
        {
            var folders = job.IncludeSubfolders
                ? "Its subfolders are removed as they are emptied, and so is the source folder itself."
                : "Only the files directly in the source folder are moved; subfolders are left alone.";
            deletionsConfirmed = await _dialogs.ConfirmAsync(
                "Move will delete the source files",
                $"Files are copied to\n{job.Destination}\nand then DELETED from\n{job.Source}\n\n" +
                folders + " Files that already exist unchanged in the destination are copied again " +
                "so they can be removed from the source.\n\nDo you want to continue?",
                confirmText: "Move",
                destructive: true);
            if (!deletionsConfirmed) return;
        }

        var logPath = job.WriteLog ? LogFileNames.Create(_paths.LogsDirectory, job.Name, DateTime.Now) : null;
        _currentRun = RunJobAsync(job, logPath, deletionsConfirmed, testRun: false);
        await _currentRun;
    }

    /// <summary>Runs the copy command with /L: shows what would be copied or deleted, changes nothing.</summary>
    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task TestRunAsync()
    {
        if (ValidatedJob() is not { } job) return;

        var logPath = job.WriteLog ? LogFileNames.Create(_paths.LogsDirectory, job.Name + " test", DateTime.Now) : null;
        _currentRun = RunJobAsync(job, logPath, deletionsConfirmed: false, testRun: true);
        await _currentRun;
    }

    /// <summary>The job from the form, or null after showing why it cannot run.</summary>
    private CopyJob? ValidatedJob()
    {
        if (Threads is null || Retries is null || WaitSeconds is null)
        {
            ShowStatus(InfoBarSeverity.Error, "Cannot start", "Enter a number for threads, retries and wait seconds.");
            return null;
        }

        var job = BuildJob();
        var problems = JobValidator.Validate(job);
        if (problems.Count > 0)
        {
            ShowStatus(InfoBarSeverity.Error, "Cannot start", string.Join(Environment.NewLine, problems));
            return null;
        }
        return job;
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop() => _stopSource?.Cancel();

    /// <summary>Stops the current run, if any, and waits until robocopy has exited.</summary>
    public async Task StopAndWaitAsync()
    {
        _stopSource?.Cancel();
        if (_currentRun is { } run) await run;
    }

    /// <summary>Asks whether to stop a running copy before the window closes.</summary>
    public Task<bool> ConfirmStopForExitAsync() => _dialogs.ConfirmAsync(
        "A copy is running",
        "Stop the copy and exit? You can resume it later; files already copied will be skipped.",
        confirmText: "Stop and exit");

    // ---- Run ---------------------------------------------------------------------------------

    private async Task RunJobAsync(CopyJob job, string? logPath, bool deletionsConfirmed, bool testRun)
    {
        var tracker = new CopyProgressTracker();
        _isTestRun = testRun;
        using var stopSource = new CancellationTokenSource();
        using var uiLoopStop = new CancellationTokenSource();

        _tracker = tracker;
        _stopSource = stopSource;
        _errorTotal = 0;
        Errors.Clear();
        UpdateErrorsHeader();
        IsRunning = true;
        IsStatusOpen = false;
        _runClock.Restart();
        var startedAt = DateTimeOffset.Now;
        var outcome = RunOutcome.Failed;
        int? exitCode = null;
        if (job.KeepAwake) _keepAwake.Acquire();

        var uiLoop = RunUiLoopAsync(uiLoopStop.Token);
        try
        {
            // A test run is itself a listing, so it needs no pre-scan.
            if (job.PreScan && !testRun)
            {
                tracker.SetPhase(CopyPhase.Scanning);
                var scan = await _runner.ScanAsync(job, tracker, stopSource.Token);
                if (scan.Run.StoppedByUser)
                {
                    tracker.CompletePass(stoppedByUser: true);
                    outcome = RunOutcome.Stopped;
                    ShowStopped();
                    return;
                }
                tracker.SetTotals(scan.FileCount, scan.TotalBytes);
            }

            tracker.SetPhase(CopyPhase.Copying);
            var pass = testRun ? RobocopyPass.TestRun : RobocopyPass.Copy;
            var result = await _runner.RunAsync(job, pass, tracker, logPath, deletionsConfirmed, stopSource.Token);
            tracker.CompletePass(result.StoppedByUser);

            if (result.Interpretation is { } interpretation)
            {
                exitCode = interpretation.Code;
                outcome = interpretation.Severity switch
                {
                    RunSeverity.Success => RunOutcome.Completed,
                    RunSeverity.Failure => RunOutcome.CompletedWithErrors,
                    _ => RunOutcome.Failed,
                };
            }
            else if (result.StoppedByUser)
            {
                outcome = RunOutcome.Stopped;
            }

            if (result.StoppedByUser) ShowStopped();
            else if (testRun) ShowTestRunResult(job, tracker.GetSnapshot(_runClock.Elapsed), result.Interpretation!, logPath);
            else ShowResult(result.Interpretation!, logPath);
        }
        catch (Exception ex)
        {
            tracker.CompletePass(stoppedByUser: true);
            if (!testRun) StartButtonText = "Start";
            _finishedText = "Failed";
            ShowStatus(InfoBarSeverity.Error, "Could not run robocopy", ex.Message);
        }
        finally
        {
            _runClock.Stop();
            await uiLoopStop.CancelAsync();
            await uiLoop;
            if (job.KeepAwake) _keepAwake.Release();
            RefreshProgress();
            (TaskbarState, TaskbarValue) = outcome switch
            {
                RunOutcome.Completed => (TaskbarItemProgressState.Normal, 1d),
                RunOutcome.Stopped => (TaskbarItemProgressState.Paused, Math.Max(TaskbarValue, 0.02)),
                _ => (TaskbarItemProgressState.Error, 1d),
            };
            BarStatusText = $"{StatusTitle} · {ElapsedText}";
            var hasLog = logPath is not null && File.Exists(logPath);
            if (hasLog) LastLogPath = logPath;
            _stopSource = null;
            IsRunning = false;

            var s = tracker.GetSnapshot(_runClock.Elapsed);
            await _history.AddAsync(new RunRecord
            {
                StartedAt = startedAt,
                Duration = _runClock.Elapsed,
                IsTestRun = testRun,
                Outcome = outcome,
                ExitCode = exitCode,
                FilesDone = s.FilesDone,
                BytesDone = s.BytesDone,
                TotalFiles = s.TotalFiles,
                TotalBytes = s.TotalBytes,
                FailedFiles = s.FailedFiles,
                ExtraFiles = s.ExtraFiles,
                ErrorCount = _errorTotal,
                LogPath = hasLog ? logPath : null,
                Job = job,
            });
        }
    }

    /// <summary>Puts a past run's settings back into the form. Returns false while a copy is running.</summary>
    public bool LoadFromHistory(RunRecord run)
    {
        if (IsRunning) return false;

        LoadFrom(run.Job);
        StartButtonText = run.Outcome == RunOutcome.Stopped && !run.IsTestRun ? "Resume" : "Start";
        ShowStatus(InfoBarSeverity.Informational, "Loaded from history",
            $"Settings from the run on {run.StartedAt.LocalDateTime:g}. Press {StartButtonText} to run it again; " +
            "files that are already copied are skipped.");
        return true;
    }

    /// <summary>Refreshes the progress card on the UI thread at a fixed rate, independent of how fast robocopy writes.</summary>
    private async Task RunUiLoopAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(UiInterval);
        try
        {
            RefreshProgress();
            while (await timer.WaitForNextTickAsync(token)) RefreshProgress();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void RefreshProgress()
    {
        if (_tracker is not { } tracker) return;
        var s = tracker.GetSnapshot(_runClock.Elapsed);

        PhaseText = s.Phase switch
        {
            CopyPhase.Scanning => "Scanning",
            CopyPhase.Copying => _isTestRun ? "Test run" : "Copying",
            CopyPhase.Finished => _finishedText,
            _ => "Idle",
        };

        if (s.Phase == CopyPhase.Scanning)
        {
            IsProgressIndeterminate = true;
            PercentText = "";
            FilesText = $"{s.ScannedFiles:N0} found";
            BytesText = DisplayFormat.Bytes(s.ScannedBytes) + " found";
            SpeedText = "–";
            EtaText = "–";
            TaskbarState = TaskbarItemProgressState.Indeterminate;
        }
        else
        {
            var percent = s.Percent;
            IsProgressIndeterminate = percent is null && s.Phase == CopyPhase.Copying;
            ProgressPercent = percent ?? 0;
            PercentText = percent is null ? "" : $"{percent:0.0} %";
            FilesText = s.TotalFiles is { } tf ? $"{s.FilesDone:N0} / {tf:N0}" : $"{s.FilesDone:N0}";
            if (s.FailedFiles > 0) FilesText += $"  ({s.FailedFiles:N0} failed)";
            BytesText = s.TotalBytes is { } tb
                ? $"{DisplayFormat.Bytes(s.BytesDone)} / {DisplayFormat.Bytes(tb)}"
                : DisplayFormat.Bytes(s.BytesDone);
            SpeedText = s.BytesPerSecond is { } bps ? DisplayFormat.Speed(bps) : "–";
            EtaText = s.Eta is { } eta ? DisplayFormat.Duration(eta) : "–";

            if (s.Phase == CopyPhase.Copying)
            {
                TaskbarState = percent is null ? TaskbarItemProgressState.Indeterminate
                    : s.FailedFiles > 0 ? TaskbarItemProgressState.Error
                    : TaskbarItemProgressState.Normal;
                TaskbarValue = Math.Clamp((percent ?? 0) / 100d, 0, 1);
            }
        }

        ElapsedText = DisplayFormat.Duration(_runClock.Elapsed);

        if (s.Phase is CopyPhase.Scanning or CopyPhase.Copying)
        {
            BarStatusSeverity = InfoBarSeverity.Informational;
            BarStatusText = string.Join(" · ", new[]
            {
                PhaseText,
                s.Phase == CopyPhase.Scanning ? FilesText : PercentText,
                EtaText == "–" ? "" : $"{EtaText} left",
            }.Where(part => part.Length > 0));
        }
        CurrentFile = s.Phase is CopyPhase.Finished ? "" : s.CurrentFile ?? "";

        var newErrors = tracker.DrainErrors(MaxErrorLines, out var dropped);
        if (newErrors.Count > 0)
        {
            _errorTotal += newErrors.Count + dropped;
            foreach (var e in newErrors)
                Errors.Add(e.Message is null ? e.Line : $"{e.Line} — {e.Message}");
            while (Errors.Count > MaxErrorLines) Errors.RemoveAt(0);
            UpdateErrorsHeader();
        }
    }

    private void UpdateErrorsHeader() =>
        ErrorsHeader = _errorTotal > Errors.Count
            ? $"Errors ({_errorTotal:N0}, showing the last {Errors.Count:N0})"
            : $"Errors ({_errorTotal:N0})";

    private void ShowStopped()
    {
        _finishedText = "Stopped";
        if (_isTestRun)
        {
            ShowStatus(InfoBarSeverity.Warning, "Test run stopped", "Stopped by you. Nothing was changed.");
            return;
        }

        StartButtonText = "Resume";
        ShowStatus(InfoBarSeverity.Warning, "Stopped",
            "Stopped by you. Press Resume to continue: files already copied are skipped and partly copied files are copied again.");
    }

    /// <summary>Summarizes what the real run would do. The exit code of a /L run describes the would-be copy.</summary>
    private void ShowTestRunResult(CopyJob job, ProgressSnapshot s, ExitCodeResult result, string? logPath)
    {
        _finishedText = "Test run finished";
        var nl = Environment.NewLine;

        var message = s.FilesDone == 0
            ? "Nothing would be copied: the destination is already up to date."
            : $"{(job.Mode == CopyMode.Move ? "Would move" : "Would copy")} {s.FilesDone:N0} files ({DisplayFormat.Bytes(s.BytesDone)}).";
        if (job.Mode == CopyMode.Move && s.FilesDone > 0)
            message += " They would be deleted from the source after copying.";

        if (s.ExtraFiles > 0)
        {
            message += nl + (job.Mode == CopyMode.Mirror
                ? $"Would DELETE {s.ExtraFiles:N0} files ({DisplayFormat.Bytes(s.ExtraBytes)}) from the destination that are not in the source."
                : $"The destination has {s.ExtraFiles:N0} files that are not in the source. They are kept.");
        }

        if (result.Severity != RunSeverity.Success)
            message += nl + "Robocopy reported errors while listing. See the errors list.";

        message += nl + "Nothing was changed.";
        if (logPath is not null && File.Exists(logPath)) message += $"{nl}Full list: {logPath}";

        var severity = result.Severity == RunSeverity.Success ? InfoBarSeverity.Informational : InfoBarSeverity.Warning;
        ShowStatus(severity, "Test run finished", message);
    }

    private void ShowResult(ExitCodeResult result, string? logPath)
    {
        StartButtonText = "Start";
        var (severity, title) = result.Severity switch
        {
            RunSeverity.Success => (InfoBarSeverity.Success, "Completed"),
            RunSeverity.Failure => (InfoBarSeverity.Error, "Completed with errors"),
            _ => (InfoBarSeverity.Error, "Failed"),
        };
        _finishedText = title;
        var message = $"{result.Message} (exit code {result.Code})";
        if (logPath is not null && File.Exists(logPath)) message += $"{Environment.NewLine}Log: {logPath}";
        ShowStatus(severity, title, message);
    }

    private void ShowStatus(InfoBarSeverity severity, string title, string message)
    {
        BarStatusSeverity = severity;
        BarStatusText = title;
        StatusSeverity = severity;
        StatusTitle = title;
        StatusMessage = message;
        IsStatusOpen = true;
    }

    // ---- Job <-> properties ------------------------------------------------------------------

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is { } name && JobProperties.Contains(name)) UpdateCommandPreview();
    }

    private void LoadFrom(CopyJob job)
    {
        Source = job.Source;
        Destination = job.Destination;
        IncludeSubfolders = job.IncludeSubfolders;
        Restartable = job.Restartable;
        SkipOlder = job.SkipOlder;
        Mode = job.Mode;
        Threads = job.Threads;
        Retries = job.Retries;
        WaitSeconds = job.WaitSeconds;
        ExcludeFiles = string.Join("; ", job.ExcludeFiles);
        ExcludeDirs = string.Join("; ", job.ExcludeDirs);
        PreScan = job.PreScan;
        WriteLog = job.WriteLog;
        KeepAwake = job.KeepAwake;
        UpdateCommandPreview();
    }

    /// <summary>A new job from the current form values, so a running job never changes under robocopy.</summary>
    private CopyJob BuildJob() => new()
    {
        Id = _job.Id,
        Name = _job.Name,
        Source = Source.Trim(),
        Destination = Destination.Trim(),
        IncludeSubfolders = IncludeSubfolders,
        Restartable = Restartable,
        SkipOlder = SkipOlder,
        Mode = Mode,
        Threads = Threads ?? 0,
        Retries = Retries ?? 0,
        WaitSeconds = WaitSeconds ?? 0,
        ExcludeFiles = SplitList(ExcludeFiles),
        ExcludeDirs = SplitList(ExcludeDirs),
        PreScan = PreScan,
        WriteLog = WriteLog,
        KeepAwake = KeepAwake,
    };

    private static List<string> SplitList(string text) =>
        text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private void UpdateCommandPreview()
    {
        var job = BuildJob();
        // Placeholders keep the preview readable before folders are chosen.
        if (job.Source.Length == 0) job.Source = "<source>";
        if (job.Destination.Length == 0) job.Destination = "<destination>";

        var logPath = job.WriteLog ? LogFileNames.Create(_paths.LogsDirectory, job.Name, DateTime.Now) : null;
        CommandPreview = "robocopy " + RobocopyCommandBuilder.Build(job, RobocopyPass.Copy, logPath);
    }
}
