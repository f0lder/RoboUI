using System.Diagnostics;
using System.Globalization;
using System.Text;
using RoboUI.Core.Models;

namespace RoboUI.Core.Robocopy;

/// <summary>Outcome of one robocopy pass.</summary>
/// <param name="ExitCode">Robocopy's exit code; meaningless when <paramref name="StoppedByUser"/> is true.</param>
/// <param name="StoppedByUser">The run was cancelled and robocopy was killed.</param>
public sealed record RunResult(int ExitCode, bool StoppedByUser, TimeSpan Duration)
{
    /// <summary>The decoded exit code, or null when the user stopped the run.</summary>
    public ExitCodeResult? Interpretation =>
        StoppedByUser || ExitCode < 0 ? null : ExitCodeInterpreter.Interpret(ExitCode);
}

/// <summary>Totals from a scan (/L) pass. Extras are not counted.</summary>
public sealed record ScanResult(long FileCount, long TotalBytes, RunResult Run);

/// <summary>Runs robocopy.exe and streams parsed output.</summary>
public sealed class RobocopyRunner
{
    static RobocopyRunner()
    {
        // Robocopy writes console output in the OEM code page (e.g. 437/850/852), which .NET Core
        // does not provide without this provider.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private readonly string _robocopyPath;

    public RobocopyRunner(string? robocopyPath = null)
    {
        _robocopyPath = robocopyPath ?? Path.Combine(Environment.SystemDirectory, "robocopy.exe");
    }

    /// <summary>
    /// Runs one pass. Events are reported on a background thread, one call per file or error,
    /// so callers should aggregate and throttle rather than pass a UI-bound Progress&lt;T&gt;.
    /// Cancelling kills robocopy; the method then returns normally with
    /// <see cref="RunResult.StoppedByUser"/> set instead of throwing.
    /// </summary>
    /// <param name="deletionsConfirmed">
    /// Must be true to run a copy pass for a job with Mirror (deletes in the destination) or Move
    /// (deletes in the source). The caller sets it only after the user confirmed a warning that
    /// names the affected folder. Not needed for scan or test-run passes, which use /L.
    /// </param>
    public Task<RunResult> RunAsync(
        CopyJob job,
        RobocopyPass pass,
        IProgress<RobocopyEvent>? events,
        string? logPath = null,
        bool deletionsConfirmed = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (job.Mode.DeletesFiles() && pass == RobocopyPass.Copy && !deletionsConfirmed)
            throw new InvalidOperationException("Mirror or Move was not confirmed by the user; refusing to delete files.");

        if (logPath is not null && pass != RobocopyPass.Scan && job.WriteLog)
        {
            var dir = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        }

        var arguments = RobocopyCommandBuilder.Build(job, pass, logPath);
        if (pass != RobocopyPass.Copy && !arguments.Split(' ').Contains("/L"))
            throw new InvalidOperationException("A scan or test run must be list-only (/L).");
        // Task.Run guarantees no part of starting or reading the process happens on the caller's (UI) thread.
        return Task.Run(() => RunProcessAsync(arguments, events, cancellationToken), CancellationToken.None);
    }

    /// <summary>Runs the scan (/L) pass and counts the files and bytes the copy pass will transfer.</summary>
    public async Task<ScanResult> ScanAsync(
        CopyJob job,
        IProgress<RobocopyEvent>? events = null,
        CancellationToken cancellationToken = default)
    {
        var counter = new ScanCounter(events);
        var run = await RunAsync(job, RobocopyPass.Scan, counter, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new ScanResult(counter.Files, counter.Bytes, run);
    }

    private async Task<RunResult> RunProcessAsync(
        string arguments,
        IProgress<RobocopyEvent>? events,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        if (cancellationToken.IsCancellationRequested)
            return new RunResult(-1, StoppedByUser: true, stopwatch.Elapsed);

        var oem = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
        var startInfo = new ProcessStartInfo(_robocopyPath, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = oem,
            StandardErrorEncoding = oem,
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        await using var registration = cancellationToken.Register(() => Kill(process));

        var stderrTask = PumpStdErrAsync(process.StandardError, events);

        var parser = new RobocopyOutputParser();
        while (await process.StandardOutput.ReadLineAsync(CancellationToken.None).ConfigureAwait(false) is { } line)
        {
            if (parser.ParseLine(line) is { } evt) events?.Report(evt);
        }
        if (parser.Flush() is { } pending) events?.Report(pending);

        await stderrTask.ConfigureAwait(false);
        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

        var stopped = cancellationToken.IsCancellationRequested;
        return new RunResult(process.ExitCode, stopped, stopwatch.Elapsed);
    }

    private static async Task PumpStdErrAsync(StreamReader reader, IProgress<RobocopyEvent>? events)
    {
        while (await reader.ReadLineAsync(CancellationToken.None).ConfigureAwait(false) is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
                events?.Report(new ErrorEvent(line.Trim(), Code: null, Path: null, Message: null));
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Exiting at the same moment; nothing left to kill.
        }
    }

    /// <summary>Counts scan totals synchronously on the reader thread, then forwards events.</summary>
    private sealed class ScanCounter(IProgress<RobocopyEvent>? inner) : IProgress<RobocopyEvent>
    {
        public long Files { get; private set; }
        public long Bytes { get; private set; }

        public void Report(RobocopyEvent value)
        {
            if (value is FileEvent { IsExtra: false } file)
            {
                Files++;
                Bytes += file.Size;
            }
            inner?.Report(value);
        }
    }
}
