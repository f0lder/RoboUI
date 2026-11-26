using System.Text;
using RoboUI.Core.Models;

namespace RoboUI.Core.Robocopy;

public enum RobocopyPass
{
    /// <summary>Listing only (/L): counts the files and bytes the copy pass will transfer. Never deletes.</summary>
    Scan,

    /// <summary>The real copy.</summary>
    Copy,

    /// <summary>
    /// The exact copy command plus /L: robocopy lists what it would copy and delete (including
    /// /MIR extras and /MOVE) without changing anything.
    /// </summary>
    TestRun,
}

/// <summary>Turns a <see cref="CopyJob"/> into a robocopy argument string.</summary>
public static class RobocopyCommandBuilder
{
    /// <summary>Switches that make the output machine-readable. /NP is required: percentages use carriage returns.</summary>
    public const string OutputSwitches = "/BYTES /FP /NDL /NP /NJH";

    /// <summary>Extra switches for the scan pass.</summary>
    public const string ScanSwitches = "/L /NJS";

    /// <summary>List-only switch for the test run.</summary>
    public const string TestRunSwitches = "/L";

    /// <summary>
    /// Added with Move: also take files robocopy considers same, tweaked or modified. Without
    /// these, a file that was copied but not yet deleted when a move was stopped would be skipped
    /// on resume and stay in the source forever.
    /// </summary>
    public const string MoveIncludeSwitches = "/IS /IT /IM";

    /// <summary>
    /// Builds the arguments for one pass.
    /// The scan pass never contains /MIR or /MOVE (it uses /E instead; the listing is the same
    /// apart from extras, which are ignored) and never writes a log. The test run is the copy
    /// command plus /L, so it shows exactly what the copy would do. <paramref name="logPath"/> is
    /// used on the copy and test-run passes when <see cref="CopyJob.WriteLog"/> is set.
    /// </summary>
    public static string Build(CopyJob job, RobocopyPass pass, string? logPath = null)
    {
        ArgumentNullException.ThrowIfNull(job);
        var listOnly = pass == RobocopyPass.Scan;

        var sb = new StringBuilder();
        sb.Append(QuotePath(job.Source)).Append(' ').Append(QuotePath(job.Destination));

        if (job.Mode == CopyMode.Mirror && !listOnly)
            sb.Append(" /MIR");
        else if (job.IncludeSubfolders || job.Mode == CopyMode.Mirror)
            sb.Append(" /E");

        if (job.Mode == CopyMode.Move)
        {
            if (!listOnly) sb.Append(" /MOVE");
            sb.Append(' ').Append(MoveIncludeSwitches);
        }

        if (job.Restartable) sb.Append(" /Z");
        if (job.SkipOlder) sb.Append(" /XO");

        sb.Append(" /MT:").Append(job.Threads);
        sb.Append(" /R:").Append(job.Retries);
        sb.Append(" /W:").Append(job.WaitSeconds);

        AppendList(sb, "/XF", job.ExcludeFiles, isPath: false);
        AppendList(sb, "/XD", job.ExcludeDirs, isPath: true);

        sb.Append(' ').Append(OutputSwitches);

        if (pass == RobocopyPass.Scan)
        {
            sb.Append(' ').Append(ScanSwitches);
            return sb.ToString();
        }

        if (pass == RobocopyPass.TestRun)
            sb.Append(' ').Append(TestRunSwitches);

        if (job.WriteLog && !string.IsNullOrWhiteSpace(logPath))
            sb.Append(" /UNILOG+:").Append(QuotePath(logPath)).Append(" /TEE");

        return sb.ToString();
    }

    /// <summary>
    /// Formats a path for the robocopy command line.
    /// A quoted path ending in a backslash breaks parsing ("D:\" escapes the quote), so trailing
    /// backslashes are trimmed; drive roots become D:\ (never quoted, they contain no spaces);
    /// only paths that contain whitespace are quoted.
    /// </summary>
    public static string QuotePath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var p = path.Trim().TrimEnd('\\', '/');
        if (p.Length == 2 && p[1] == ':' && char.IsAsciiLetter(p[0]))
            return char.ToUpperInvariant(p[0]) + @":\";

        return QuoteIfNeeded(p);
    }

    private static string QuoteIfNeeded(string value) =>
        value.Any(char.IsWhiteSpace) ? $"\"{value}\"" : value;

    private static void AppendList(StringBuilder sb, string flag, IEnumerable<string>? items, bool isPath)
    {
        if (items is null) return;

        var cleaned = items
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(i => isPath ? QuotePath(i) : QuoteIfNeeded(i.Trim()))
            .ToList();
        if (cleaned.Count == 0) return;

        sb.Append(' ').Append(flag);
        foreach (var item in cleaned) sb.Append(' ').Append(item);
    }
}
