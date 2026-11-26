using RoboUI.Core.Models;

namespace RoboUI.Core.History;

public enum RunOutcome
{
    /// <summary>Robocopy finished and everything that should be copied was copied (exit code 0–7).</summary>
    Completed,

    /// <summary>Robocopy finished but some files could not be copied (exit code 8–15).</summary>
    CompletedWithErrors,

    /// <summary>Robocopy could not run or reported a fatal error (exit code 16+).</summary>
    Failed,

    /// <summary>The user stopped the run.</summary>
    Stopped,
}

/// <summary>One finished run (copy or test run), as kept in the history.</summary>
public sealed class RunRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset StartedAt { get; set; }
    public TimeSpan Duration { get; set; }

    /// <summary>A /L run: nothing was changed; the counts are what would have been copied.</summary>
    public bool IsTestRun { get; set; }

    public RunOutcome Outcome { get; set; }

    /// <summary>Robocopy's exit code; null when the run was stopped or robocopy could not start.</summary>
    public int? ExitCode { get; set; }

    public long FilesDone { get; set; }
    public long BytesDone { get; set; }
    public long? TotalFiles { get; set; }
    public long? TotalBytes { get; set; }
    public long FailedFiles { get; set; }
    public long ExtraFiles { get; set; }
    public long ErrorCount { get; set; }

    public string? LogPath { get; set; }

    /// <summary>The job's settings at the time of the run, so the run can be repeated or resumed.</summary>
    public CopyJob Job { get; set; } = new();
}
