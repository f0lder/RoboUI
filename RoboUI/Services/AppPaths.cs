using System.IO;
namespace RoboUI.Services;

/// <summary>Folders the app writes to under %APPDATA%\RoboUI.</summary>
public sealed class AppPaths
{
    public string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RoboUI");

    public string LogsDirectory => Path.Combine(DataDirectory, "logs");
}
