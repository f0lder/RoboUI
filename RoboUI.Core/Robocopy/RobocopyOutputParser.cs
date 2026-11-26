using System.Globalization;
using System.Text.RegularExpressions;

namespace RoboUI.Core.Robocopy;

/// <summary>
/// Parses robocopy output produced with /BYTES /FP /NDL /NP /NJH, one line at a time.
/// The parser is stateful (an error's message is on the next non-empty line), so use one
/// instance per robocopy run and call <see cref="Flush"/> when the output ends.
/// Not thread-safe.
/// </summary>
public sealed partial class RobocopyOutputParser
{
    // "\t    New File  \t\t   123456\tC:\full\path.ext"
    // The class text may be localized, so the line is matched by structure:
    // tab, class text, tab(s), digits, tab, absolute path (drive or UNC) not ending in '\'.
    [GeneratedRegex(@"^\t(?<class>[^\t]*)\t+ *(?<size>\d+)\t(?<path>(?:[A-Za-z]:\\|\\\\)[^\t]*[^\t\\])$")]
    private static partial Regex FileLineRegex();

    // "2025/11/26 10:00:00 ERROR 5 (0x00000005) Copying File C:\src\a.txt"
    [GeneratedRegex(@"\(0x(?<hex>[0-9A-Fa-f]{8})\)")]
    private static partial Regex ErrorCodeRegex();

    [GeneratedRegex(@"(?:[A-Za-z]:\\|\\\\).*$")]
    private static partial Regex PathRegex();

    private ErrorEvent? _pendingError;

    /// <summary>
    /// Parses one line. Returns a <see cref="FileEvent"/>, an <see cref="ErrorEvent"/>,
    /// or null when the line should be ignored.
    /// An error header is held until its message line arrives, then returned as one event.
    /// </summary>
    public RobocopyEvent? ParseLine(string? line)
    {
        if (line is null) return null;
        line = line.TrimEnd('\r', '\n');

        if (_pendingError is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) return null;
            var completed = _pendingError with { Message = line.Trim() };
            _pendingError = null;
            return completed;
        }

        if (string.IsNullOrWhiteSpace(line)) return null;

        var errorMatch = ErrorCodeRegex().Match(line);
        if (errorMatch.Success)
        {
            var code = int.Parse(errorMatch.Groups["hex"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var rest = line[(errorMatch.Index + errorMatch.Length)..];
            var pathMatch = PathRegex().Match(rest);
            _pendingError = new ErrorEvent(
                Line: line.Trim(),
                Code: code,
                Path: pathMatch.Success ? pathMatch.Value.Trim() : null,
                Message: null);
            return null;
        }

        if (line.Contains("RETRY LIMIT EXCEEDED", StringComparison.OrdinalIgnoreCase))
            return new ErrorEvent(line.Trim(), Code: null, Path: null, Message: null, IsRetryLimitExceeded: true);

        var fileMatch = FileLineRegex().Match(line);
        if (fileMatch.Success &&
            long.TryParse(fileMatch.Groups["size"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var size))
        {
            return new FileEvent(fileMatch.Groups["class"].Value.Trim(), size, fileMatch.Groups["path"].Value);
        }

        return null;
    }

    /// <summary>Returns an error header still waiting for its message (output ended early), if any.</summary>
    public ErrorEvent? Flush()
    {
        var pending = _pendingError;
        _pendingError = null;
        return pending;
    }
}
