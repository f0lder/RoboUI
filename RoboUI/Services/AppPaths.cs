using System.IO;
namespace RoboUI.Services;

/// <summary>
/// Folders the app writes to: %APPDATA%\RoboUI by default. Setting the environment variable
/// ROBOUI_DATA_DIR to a folder uses that instead (for testing, demos and screenshots).
/// </summary>
public sealed class AppPaths
{
    public const string DataDirOverrideVariable = "ROBOUI_DATA_DIR";

    public string DataDirectory { get; } =
        Environment.GetEnvironmentVariable(DataDirOverrideVariable) is { Length: > 0 } custom
            ? Path.GetFullPath(custom)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RoboUI");

    public string LogsDirectory => Path.Combine(DataDirectory, "logs");

    public string HistoryFile => Path.Combine(DataDirectory, "history.json");

    public string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    /// <summary>A copy of the app icon on disk, because Windows notifications need an icon file.</summary>
    public string NotificationIconFile => Path.Combine(DataDirectory, "RoboUI.png");
}
