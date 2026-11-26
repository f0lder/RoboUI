using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoboUI.Services;

namespace RoboUI.ViewModels;

public sealed partial class AboutViewModel(IDialogService dialogs, AppPaths paths) : ObservableObject
{
    /// <summary>The app version without the build metadata suffix ("1.2.3+abc" → "1.2.3").</summary>
    public string Version { get; } =
        (Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
         ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
         ?? "unknown").Split('+')[0];

    public string Runtime { get; } = RuntimeInformation.FrameworkDescription;

    public string RobocopyPath { get; } = Path.Combine(Environment.SystemDirectory, "robocopy.exe");

    public string LogsFolder => paths.LogsDirectory;

    [RelayCommand]
    private void OpenLogsFolder()
    {
        Directory.CreateDirectory(paths.LogsDirectory);
        dialogs.OpenInShell(paths.LogsDirectory);
    }
}
