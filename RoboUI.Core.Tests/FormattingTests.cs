using System.Globalization;
using RoboUI.Core.Formatting;
using RoboUI.Core.Logging;

namespace RoboUI.Core.Tests;

public class FormattingTests
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1.00 KB")]
    [InlineData(1536, "1.50 KB")]
    [InlineData(15 * 1024 * 1024, "15.0 MB")]
    [InlineData(250L * 1024 * 1024 * 1024, "250 GB")]
    [InlineData(3L * 1024 * 1024 * 1024 * 1024, "3.00 TB")]
    public void Bytes(long value, string expected) => Assert.Equal(expected, DisplayFormat.Bytes(value, Inv));

    [Fact]
    public void Speed() => Assert.Equal("1.00 MB/s", DisplayFormat.Speed(1024 * 1024, Inv));

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(59, "0:59")]
    [InlineData(61, "1:01")]
    [InlineData(3600, "1:00:00")]
    [InlineData(90061, "25:01:01")]
    [InlineData(-5, "0:00")]
    public void Duration(int seconds, string expected) =>
        Assert.Equal(expected, DisplayFormat.Duration(TimeSpan.FromSeconds(seconds)));

    [Theory]
    [InlineData("Photos 2024", "Photos_2024")]
    [InlineData("a/b:c*?", "a_b_c")]
    [InlineData("  ", "job")]
    [InlineData(null, "job")]
    [InlineData("..", "job")]
    public void SafeName(string? name, string expected) => Assert.Equal(expected, LogFileNames.SafeName(name));

    [Fact]
    public void Log_file_name_has_job_and_timestamp()
    {
        var path = LogFileNames.Create(@"C:\logs", "My Job", new DateTime(2026, 11, 26, 9, 5, 7));
        Assert.Equal(@"C:\logs\My_Job_20261126_090507.log", path);
    }
}
