using System.Globalization;
using System.Text;

namespace RoboUI.Core.Logging;

/// <summary>Builds log file paths of the form &lt;job&gt;_&lt;yyyyMMdd_HHmmss&gt;.log.</summary>
public static class LogFileNames
{
    public static string Create(string logsDirectory, string jobName, DateTime timestamp) =>
        Path.Combine(logsDirectory,
            $"{SafeName(jobName)}_{timestamp.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}.log");

    /// <summary>Makes a job name safe to use in a file name. Spaces become '_' so log paths need no quoting.</summary>
    public static string SafeName(string? jobName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder();
        foreach (var c in (jobName ?? "").Trim())
            sb.Append(invalid.Contains(c) || char.IsWhiteSpace(c) ? '_' : c);

        var result = sb.ToString().Trim('.', '_');
        return result.Length == 0 ? "job" : result;
    }
}
