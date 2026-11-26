using RoboUI.Core.Progress;

namespace RoboUI.Core.Tests;

public class SpeedMeterTests
{
    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    [Fact]
    public void Null_until_one_second_of_samples()
    {
        var m = new SpeedMeter(S(20));
        Assert.Null(m.BytesPerSecond);
        m.AddSample(S(0), 0);
        Assert.Null(m.BytesPerSecond);
        m.AddSample(S(0.5), 500);
        Assert.Null(m.BytesPerSecond);
        m.AddSample(S(1), 1000);
        Assert.Equal(1000, m.BytesPerSecond);
    }

    [Fact]
    public void Only_the_last_window_counts()
    {
        var m = new SpeedMeter(S(20));
        // 10 MB in the first 10 s, then nothing for 30 s.
        m.AddSample(S(0), 0);
        m.AddSample(S(10), 10_000_000);
        for (var t = 11; t <= 40; t++) m.AddSample(S(t), 10_000_000);

        Assert.Equal(0, m.BytesPerSecond);
    }

    [Fact]
    public void Window_average_while_partially_filled()
    {
        var m = new SpeedMeter(S(20));
        for (var t = 0; t <= 5; t++) m.AddSample(S(t), t * 200);
        Assert.Equal(200, m.BytesPerSecond);
    }

    [Fact]
    public void Steady_rate_over_long_run()
    {
        var m = new SpeedMeter(S(20));
        for (var t = 0.0; t <= 120; t += 0.25) m.AddSample(S(t), (long)(t * 4000));
        Assert.Equal(4000, m.BytesPerSecond!.Value, precision: 6);
    }

    [Fact]
    public void Reset_clears_samples()
    {
        var m = new SpeedMeter(S(20));
        m.AddSample(S(0), 0);
        m.AddSample(S(5), 5000);
        m.Reset();
        Assert.Null(m.BytesPerSecond);
    }
}
