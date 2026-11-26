using System.Collections.Concurrent;
using RoboUI.Core.Robocopy;

namespace RoboUI.Core.Progress;

public enum CopyPhase
{
    Idle,
    Scanning,
    Copying,
    Finished,
}

/// <summary>A consistent view of the progress at one moment, for the UI.</summary>
public sealed record ProgressSnapshot(
    CopyPhase Phase,
    long ScannedFiles,
    long ScannedBytes,
    long FilesDone,
    long BytesDone,
    long FailedFiles,
    long? TotalFiles,
    long? TotalBytes,
    string? CurrentFile,
    double? BytesPerSecond,
    TimeSpan? Eta,
    long ExtraFiles = 0,
    long ExtraBytes = 0)
{
    /// <summary>0–100, or null when totals are unknown (no pre-scan).</summary>
    public double? Percent
    {
        get
        {
            double? value = TotalBytes > 0 ? 100.0 * BytesDone / TotalBytes.Value
                : TotalFiles > 0 ? 100.0 * FilesDone / TotalFiles.Value
                : TotalFiles == 0 ? 100.0
                : null;
            return value is null ? null : Math.Clamp(value.Value, 0, 100);
        }
    }
}

/// <summary>
/// Collects robocopy events (from the runner's background thread) into counters the UI reads on a
/// timer. A file's bytes are credited only when the next file starts or the pass completes, so the
/// progress never runs ahead of what is really on disk. All members are thread-safe.
/// </summary>
public sealed class CopyProgressTracker : IProgress<RobocopyEvent>
{
    public const int DefaultSpeedWindowSeconds = 20;

    private readonly Lock _gate = new();
    private readonly ConcurrentQueue<ErrorEvent> _errors = new();
    private readonly SpeedMeter _speed;

    private CopyPhase _phase = CopyPhase.Idle;
    private long _scannedFiles, _scannedBytes;
    private long _filesDone, _bytesDone, _failedFiles;
    private long _extraFiles, _extraBytes;
    private long? _totalFiles, _totalBytes;
    private FileEvent? _current;
    private string? _currentPath;
    private string? _lastErrorPath;

    public CopyProgressTracker(TimeSpan? speedWindow = null)
    {
        _speed = new SpeedMeter(speedWindow ?? TimeSpan.FromSeconds(DefaultSpeedWindowSeconds));
    }

    public void SetPhase(CopyPhase phase)
    {
        lock (_gate)
        {
            _phase = phase;
            if (phase == CopyPhase.Copying) _speed.Reset();
        }
    }

    /// <summary>Sets the totals measured by the scan pass.</summary>
    public void SetTotals(long files, long bytes)
    {
        lock (_gate)
        {
            _totalFiles = files;
            _totalBytes = bytes;
        }
    }

    public void Report(RobocopyEvent value)
    {
        switch (value)
        {
            case FileEvent { IsExtra: true } extra:
                // Extras are never progress. They are counted (copy and test-run passes only) so a
                // test run can report what Mirror would delete.
                lock (_gate)
                {
                    if (_phase != CopyPhase.Scanning)
                    {
                        _extraFiles++;
                        _extraBytes += extra.Size;
                    }
                }
                return;

            case FileEvent file:
                lock (_gate)
                {
                    if (_phase == CopyPhase.Scanning)
                    {
                        _scannedFiles++;
                        _scannedBytes += file.Size;
                        _currentPath = file.Path;
                        return;
                    }

                    // Robocopy may print the same file again when it retries it.
                    if (_current is not null && string.Equals(_current.Path, file.Path, StringComparison.OrdinalIgnoreCase))
                        return;

                    CreditCurrent();
                    _current = file;
                    _currentPath = file.Path;
                }
                return;

            case ErrorEvent error:
                bool scanning;
                lock (_gate)
                {
                    scanning = _phase == CopyPhase.Scanning;
                    if (!scanning)
                    {
                        if (error.Path is not null)
                        {
                            _lastErrorPath = error.Path;
                        }
                        else if (error.IsRetryLimitExceeded && _current is not null &&
                                 string.Equals(_lastErrorPath, _current.Path, StringComparison.OrdinalIgnoreCase))
                        {
                            // Robocopy gave up on this file: it was not copied, so never credit it.
                            _failedFiles++;
                            _current = null;
                        }
                    }
                }
                // Scan-pass errors repeat in the copy pass, so only copy-pass errors are shown.
                if (!scanning) _errors.Enqueue(error);
                return;
        }
    }

    /// <summary>
    /// Call when a copy pass ends. The last file is credited only if robocopy finished on its own;
    /// after a stop it may be partial.
    /// </summary>
    public void CompletePass(bool stoppedByUser)
    {
        lock (_gate)
        {
            if (!stoppedByUser) CreditCurrent();
            _current = null;
            _phase = CopyPhase.Finished;
        }
    }

    /// <summary>Takes up to <paramref name="max"/> queued errors; older ones beyond that are dropped.</summary>
    public IReadOnlyList<ErrorEvent> DrainErrors(int max, out int dropped)
    {
        var drained = new List<ErrorEvent>();
        while (_errors.TryDequeue(out var e)) drained.Add(e);
        dropped = Math.Max(0, drained.Count - max);
        return dropped > 0 ? drained.GetRange(dropped, max) : drained;
    }

    /// <summary>Returns the current progress and records a speed sample at <paramref name="now"/>.</summary>
    public ProgressSnapshot GetSnapshot(TimeSpan now)
    {
        lock (_gate)
        {
            double? speed = null;
            TimeSpan? eta = null;
            if (_phase == CopyPhase.Copying)
            {
                _speed.AddSample(now, _bytesDone);
                speed = _speed.BytesPerSecond;
                if (speed > 0 && _totalBytes is { } total)
                {
                    var remaining = Math.Max(0, total - _bytesDone);
                    eta = TimeSpan.FromSeconds(Math.Min(remaining / speed.Value, TimeSpan.MaxValue.TotalSeconds / 2));
                }
            }

            return new ProgressSnapshot(
                _phase, _scannedFiles, _scannedBytes, _filesDone, _bytesDone, _failedFiles,
                _totalFiles, _totalBytes, _currentPath, speed, eta, _extraFiles, _extraBytes);
        }
    }

    private void CreditCurrent()
    {
        if (_current is null) return;
        _filesDone++;
        _bytesDone += _current.Size;
        _current = null;
    }
}
