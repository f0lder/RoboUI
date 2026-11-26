using System.Globalization;

namespace RoboUI.Core.Formatting;

/// <summary>Human-readable sizes, speeds and durations.</summary>
public static class DisplayFormat
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB"];

    public static string Bytes(long bytes, IFormatProvider? provider = null)
    {
        provider ??= CultureInfo.CurrentCulture;
        if (bytes < 1024) return bytes.ToString(provider) + " B";

        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        var format = value >= 100 ? "0" : value >= 10 ? "0.0" : "0.00";
        return value.ToString(format, provider) + " " + Units[unit];
    }

    public static string Speed(double bytesPerSecond, IFormatProvider? provider = null) =>
        Bytes((long)Math.Round(bytesPerSecond), provider) + "/s";

    /// <summary>"m:ss" under an hour, otherwise "h:mm:ss" (hours may exceed 24).</summary>
    public static string Duration(TimeSpan span)
    {
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        var hours = (long)span.TotalHours;
        return hours > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{hours}:{span.Minutes:00}:{span.Seconds:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{span.Minutes}:{span.Seconds:00}");
    }
}
