namespace RoboUI.Core.Progress;

/// <summary>
/// Transfer speed over a moving time window, from samples of (time, total bytes done).
/// Not thread-safe; <see cref="CopyProgressTracker"/> calls it under its lock.
/// </summary>
public sealed class SpeedMeter(TimeSpan window)
{
    private static readonly TimeSpan MinimumSpan = TimeSpan.FromSeconds(1);

    private readonly List<(TimeSpan Time, long Bytes)> _samples = [];

    public TimeSpan Window { get; } = window;

    public void AddSample(TimeSpan time, long totalBytes)
    {
        _samples.Add((time, totalBytes));

        // Keep exactly one sample at or before the window start so the window stays fully covered.
        var windowStart = time - Window;
        while (_samples.Count > 2 && _samples[1].Time <= windowStart)
            _samples.RemoveAt(0);
    }

    /// <summary>Bytes per second over the window, or null until at least one second of samples exists.</summary>
    public double? BytesPerSecond
    {
        get
        {
            if (_samples.Count < 2) return null;
            var first = _samples[0];
            var last = _samples[^1];
            var span = last.Time - first.Time;
            if (span < MinimumSpan) return null;
            return Math.Max(0, last.Bytes - first.Bytes) / span.TotalSeconds;
        }
    }

    public void Reset() => _samples.Clear();
}
