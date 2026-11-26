namespace RoboUI.Core.Robocopy;

/// <summary>Something meaningful parsed from robocopy's output.</summary>
public abstract record RobocopyEvent;

/// <summary>
/// A file line. <see cref="FileClass"/> is robocopy's (possibly localized) label such as "New File".
/// </summary>
public sealed record FileEvent(string FileClass, long Size, string Path) : RobocopyEvent
{
    /// <summary>Classes starting with '*' (e.g. "*EXTRA File") are not copies and must not count as progress.</summary>
    public bool IsExtra => FileClass.StartsWith('*');
}

/// <summary>An error reported by robocopy, or a line robocopy wrote to stderr.</summary>
/// <param name="Line">The error header line, trimmed.</param>
/// <param name="Code">Win32 error code from "(0x........)", if present.</param>
/// <param name="Path">The path the error refers to, if one was found on the header line.</param>
/// <param name="Message">The explanation robocopy prints on the following line.</param>
/// <param name="IsRetryLimitExceeded">True for "RETRY LIMIT EXCEEDED" (the file was given up on).</param>
public sealed record ErrorEvent(
    string Line,
    int? Code,
    string? Path,
    string? Message,
    bool IsRetryLimitExceeded = false) : RobocopyEvent;
