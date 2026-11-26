namespace RoboUI.Core.Robocopy;

public enum RunSeverity
{
    /// <summary>Exit code 0–7: everything that should have been copied was copied.</summary>
    Success,

    /// <summary>Exit code 8–15: some files or folders could not be copied.</summary>
    Failure,

    /// <summary>Exit code 16 or higher: robocopy did not copy anything.</summary>
    Fatal,
}

/// <summary>Robocopy's exit code, decoded.</summary>
public sealed record ExitCodeResult(
    int Code,
    RunSeverity Severity,
    bool FilesCopied,
    bool ExtrasDetected,
    bool MismatchesDetected,
    bool SomeFilesFailed,
    string Message)
{
    public bool IsSuccess => Severity == RunSeverity.Success;
    public bool NothingToDo => Code == 0;
}

/// <summary>Interprets robocopy exit codes, which are bit flags: 1 copied, 2 extras, 4 mismatches, 8 failures, 16 fatal.</summary>
public static class ExitCodeInterpreter
{
    public const int Copied = 1;
    public const int Extras = 2;
    public const int Mismatches = 4;
    public const int Failures = 8;
    public const int Fatal = 16;

    public static ExitCodeResult Interpret(int code)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(code);

        var fatal = (code & Fatal) != 0;
        var failures = (code & Failures) != 0;
        var copied = (code & Copied) != 0;
        var extras = (code & Extras) != 0;
        var mismatches = (code & Mismatches) != 0;

        var severity = fatal ? RunSeverity.Fatal
            : failures ? RunSeverity.Failure
            : RunSeverity.Success;

        string message;
        if (fatal)
        {
            message = "Fatal error: robocopy did not copy any files. Check that the paths exist and that you have access.";
        }
        else if (code == 0)
        {
            message = "Nothing to copy: the destination is already up to date.";
        }
        else
        {
            var parts = new List<string>();
            if (failures) parts.Add("Some files or folders could not be copied. See the errors list.");
            if (copied) parts.Add(failures ? "The other files were copied." : "Files copied successfully.");
            else if (!failures) parts.Add("No new files were copied.");
            if (extras) parts.Add("The destination contains extra files or folders that are not in the source.");
            if (mismatches) parts.Add("Some files or folders in the destination do not match the source.");
            message = string.Join(" ", parts);
        }

        return new ExitCodeResult(code, severity, copied, extras, mismatches, failures, message);
    }
}
