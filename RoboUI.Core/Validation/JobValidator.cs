using RoboUI.Core.Models;

namespace RoboUI.Core.Validation;

/// <summary>Checks a job before it is started.</summary>
public static class JobValidator
{
    /// <summary>Returns a list of human-readable problems; empty when the job can start.</summary>
    public static IReadOnlyList<string> Validate(CopyJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        var errors = new List<string>();

        string? source = TryFullPath(job.Source);
        string? destination = TryFullPath(job.Destination);

        if (string.IsNullOrWhiteSpace(job.Source))
            errors.Add("Choose a source folder.");
        else if (source is null)
            errors.Add(@"The source must be a full path, for example D:\Data or \\server\share.");
        else if (!Directory.Exists(source))
            errors.Add("The source folder does not exist.");

        if (string.IsNullOrWhiteSpace(job.Destination))
            errors.Add("Choose a destination folder.");
        else if (destination is null)
            errors.Add(@"The destination must be a full path, for example E:\Backup or \\server\share.");
        else if (source is not null && IsSameOrInside(destination, source))
            errors.Add("The destination cannot be the source folder or a folder inside it.");

        if (job.Threads is < CopyJob.MinThreads or > CopyJob.MaxThreads)
            errors.Add($"Threads must be between {CopyJob.MinThreads} and {CopyJob.MaxThreads}.");
        if (job.Retries < 0)
            errors.Add("Retries cannot be negative.");
        if (job.WaitSeconds < 0)
            errors.Add("Wait time cannot be negative.");

        return errors;
    }

    /// <summary>True if <paramref name="path"/> equals <paramref name="parent"/> or is a folder below it.</summary>
    internal static bool IsSameOrInside(string path, string parent)
    {
        var p = WithTrailingSeparator(path);
        var root = WithTrailingSeparator(parent);
        return p.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static string WithTrailingSeparator(string path) =>
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

    private static string? TryFullPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            var trimmed = path.Trim();
            return Path.IsPathFullyQualified(trimmed) ? Path.GetFullPath(trimmed) : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
}
