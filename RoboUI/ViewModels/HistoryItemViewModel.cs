using System.IO;
using RoboUI.Core.Formatting;
using RoboUI.Core.History;
using RoboUI.Core.Models;
using Wpf.Ui.Controls;

namespace RoboUI.ViewModels;

/// <summary>One run in the History list, with its display texts prepared once.</summary>
public sealed class HistoryItemViewModel
{
    public HistoryItemViewModel(RunRecord run)
    {
        Run = run;

        Title = (run.IsTestRun, run.Outcome) switch
        {
            (true, RunOutcome.Stopped) => "Test run stopped",
            (true, RunOutcome.Failed) => "Test run failed",
            (true, _) => "Test run",
            (_, RunOutcome.Completed) => "Completed",
            (_, RunOutcome.CompletedWithErrors) => "Completed with errors",
            (_, RunOutcome.Stopped) => "Stopped",
            _ => "Failed",
        };

        Severity = run.Outcome switch
        {
            RunOutcome.Failed or RunOutcome.CompletedWithErrors => InfoBarSeverity.Error,
            RunOutcome.Stopped => InfoBarSeverity.Warning,
            _ when run.IsTestRun => InfoBarSeverity.Informational,
            _ => InfoBarSeverity.Success,
        };

        When = FormatWhen(run.StartedAt.LocalDateTime);

        var mode = run.Job.Mode switch
        {
            CopyMode.Mirror => "Mirror",
            CopyMode.Move => "Move",
            _ => "Copy",
        };
        Paths = $"{mode} · {run.Job.Source}  →  {run.Job.Destination}";

        var parts = new List<string>();
        var files = $"{Count(run.FilesDone, "file")} · {DisplayFormat.Bytes(run.BytesDone)}";
        if (run.IsTestRun)
            parts.Add((run.Job.Mode == CopyMode.Move ? "Would move " : "Would copy ") + files);
        else if (run.Outcome == RunOutcome.Stopped && run.TotalFiles is { } total)
            parts.Add($"{run.FilesDone:N0} of {Count(total, "file")} · {DisplayFormat.Bytes(run.BytesDone)}");
        else
            parts.Add(files);
        if (run.IsTestRun && run.Job.Mode == CopyMode.Mirror && run.ExtraFiles > 0)
            parts.Add($"would delete {run.ExtraFiles:N0}");
        parts.Add(DisplayFormat.Duration(run.Duration));
        if (run.FailedFiles > 0) parts.Add($"{run.FailedFiles:N0} failed");
        if (run.ErrorCount > 0) parts.Add(Count(run.ErrorCount, "error"));
        if (run.ExitCode is { } code) parts.Add($"exit code {code}");
        Summary = string.Join(" · ", parts);

        HasLog = run.LogPath is not null && File.Exists(run.LogPath);
    }

    public RunRecord Run { get; }
    public string Title { get; }
    public InfoBarSeverity Severity { get; }
    public string When { get; }
    public string Paths { get; }
    public string Summary { get; }
    public bool HasLog { get; }

    /// <summary>"1 file", "2 files", "1,234 files".</summary>
    private static string Count(long n, string noun) => $"{n:N0} {noun}{(n == 1 ? "" : "s")}";

    private static string FormatWhen(DateTime local)
    {
        var today = DateTime.Today;
        if (local.Date == today) return $"Today {local:t}";
        if (local.Date == today.AddDays(-1)) return $"Yesterday {local:t}";
        return local.ToString("g");
    }
}
