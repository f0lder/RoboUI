namespace RoboUI.Core.Models;

/// <summary>The three ways a job can treat files. Exactly one applies, so they cannot conflict.</summary>
public enum CopyMode
{
    /// <summary>Copy new and changed files. Never deletes anything.</summary>
    Copy,

    /// <summary>/MIR – make the destination an exact copy: deletes destination files that are not in the source. Always includes subfolders.</summary>
    Mirror,

    /// <summary>/MOVE – copy, then delete the files and folders from the source.</summary>
    Move,
}

public static class CopyModeExtensions
{
    /// <summary>True for modes that delete files and therefore need the user's confirmation.</summary>
    public static bool DeletesFiles(this CopyMode mode) => mode is CopyMode.Mirror or CopyMode.Move;
}
