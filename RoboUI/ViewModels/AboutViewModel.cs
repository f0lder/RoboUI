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

    public const string GitHubUrl = "https://github.com/f0lder";
    public const string BuyMeACoffeeUrl = "https://buymeacoffee.com/f0lder";

    /// <summary>Opens one of the links above in the default browser.</summary>
    [RelayCommand]
    private void OpenLink(string? url)
    {
        if (url is GitHubUrl or BuyMeACoffeeUrl) dialogs.OpenInShell(url);
    }

    [RelayCommand]
    private void OpenLogsFolder()
    {
        Directory.CreateDirectory(paths.LogsDirectory);
        dialogs.OpenInShell(paths.LogsDirectory);
    }
}
