namespace RoboUI.Core.Models;

/// <summary>A saved robocopy job. Mutable so it can be edited by the UI and serialized as JSON.</summary>
public sealed class CopyJob
{
    public const int DefaultThreads = 8;
    public const int MinThreads = 1;
    public const int MaxThreads = 128;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New job";
    public string Source { get; set; } = "";
    public string Destination { get; set; } = "";

    /// <summary>/E – copy subfolders, including empty ones.</summary>
    public bool IncludeSubfolders { get; set; } = true;

    /// <summary>/Z – restartable mode, lets robocopy resume a partially copied file.</summary>
    public bool Restartable { get; set; } = true;

    /// <summary>/XO – skip files where the destination copy is newer.</summary>
    public bool SkipOlder { get; set; }

    /// <summary>What happens to files besides copying: nothing, delete extras in the destination, or delete from the source.</summary>
    public CopyMode Mode { get; set; } = CopyMode.Copy;

    /// <summary>/MT:n</summary>
    public int Threads { get; set; } = DefaultThreads;

    /// <summary>/R:n</summary>
    public int Retries { get; set; } = 2;

    /// <summary>/W:n</summary>
    public int WaitSeconds { get; set; } = 5;

    /// <summary>/XF file names or wildcards.</summary>
    public List<string> ExcludeFiles { get; set; } = [];

    /// <summary>/XD folder names or paths.</summary>
    public List<string> ExcludeDirs { get; set; } = [];

    /// <summary>Run a /L listing pass first to get file and byte totals for progress.</summary>
    public bool PreScan { get; set; } = true;

    /// <summary>Write a Unicode log file (/UNILOG+ /TEE).</summary>
    public bool WriteLog { get; set; } = true;

    /// <summary>Keep the PC awake while the job runs.</summary>
    public bool KeepAwake { get; set; } = true;
}
