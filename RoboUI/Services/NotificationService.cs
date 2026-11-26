using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows;
using Microsoft.Win32;
using RoboUI.Core.History;
using RoboUI.ViewModels;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace RoboUI.Services;

/// <summary>
/// Shows a Windows notification when a run finishes, unless turned off in Settings. By default only
/// when RoboUI isn't the active window, and never for a run the user stopped.
///
/// RoboUI isn't a packaged app, so it registers its own identity (AppUserModelId) under
/// HKCU\Software\Classes\AppUserModelId: this gives the notification its name and icon. Clicking a
/// notification brings the window to the front while RoboUI is running.
/// </summary>
public sealed class NotificationService
{
    public const string AppId = "f0lder.RoboUI";

    private readonly SettingsService _settings;
    private readonly AppPaths _paths;
    private ToastNotifier? _notifier;

    public NotificationService(SettingsService settings, RunHistoryService history, AppPaths paths)
    {
        _settings = settings;
        _paths = paths;
        history.Added += OnRunFinished;
    }

    private void OnRunFinished(RunRecord run)
    {
        if (!_settings.Current.NotifyOnFinish || run.Outcome == RunOutcome.Stopped) return;
        if (Application.Current?.MainWindow is { IsActive: true } && !_settings.Current.NotifyWhenActive) return;

        var item = new HistoryItemViewModel(run);
        Show(item.Title, item.ShortSummary, $"{FolderName(run.Job.Source)}  →  {FolderName(run.Job.Destination)}");
    }

    /// <summary>"D:\Photos\2024" → "2024"; a drive root stays "D:\".</summary>
    private static string FolderName(string path)
    {
        var trimmed = path.TrimEnd('\\', '/');
        var name = Path.GetFileName(trimmed);
        return name.Length > 0 ? name : path;
    }

    /// <summary>Sends a sample notification. Returns false if Windows refused it.</summary>
    public bool ShowTest() =>
        Show("Notifications are on", "RoboUI will let you know here when a copy finishes.", null);

    private bool Show(string title, string body, string? detail)
    {
        try
        {
            var xml = new XmlDocument();
            xml.LoadXml(
                "<toast launch=\"open\"><visual><binding template=\"ToastGeneric\">" +
                $"<text>{SecurityElement.Escape(title)}</text>" +
                $"<text>{SecurityElement.Escape(body)}</text>" +
                (detail is null ? "" : $"<text>{SecurityElement.Escape(detail)}</text>") +
                "</binding></visual></toast>");

            var toast = new ToastNotification(xml);
            toast.Activated += (_, _) => Application.Current?.Dispatcher.BeginInvoke(BringWindowToFront);
            (_notifier ??= CreateNotifier()).Show(toast);
            return true;
        }
        catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Notifications are a convenience; a failure must never affect a run.
            return false;
        }
    }

    private ToastNotifier CreateNotifier()
    {
        Register();
        return ToastNotificationManager.CreateToastNotifier(AppId);
    }

    /// <summary>Registers the app's name and icon for notifications. Cheap, so it is refreshed every launch.</summary>
    private void Register()
    {
        Directory.CreateDirectory(_paths.DataDirectory);
        var icon = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/RoboUI.png"));
        if (icon is not null)
        {
            using var source = icon.Stream;
            using var target = File.Create(_paths.NotificationIconFile);
            source.CopyTo(target);
        }

        using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\AppUserModelId\{AppId}");
        key.SetValue("DisplayName", "RoboUI");
        key.SetValue("IconUri", _paths.NotificationIconFile);
    }

    private static void BringWindowToFront()
    {
        if (Application.Current?.MainWindow is not { } window) return;
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Activate();
    }
}
