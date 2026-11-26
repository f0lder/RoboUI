using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoboUI.Core.Settings;
using RoboUI.Services;

namespace RoboUI.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly NotificationService _notifications;
    private readonly IDialogService _dialogs;

    private readonly ThemeService _theme;

    public SettingsViewModel(SettingsService settings, NotificationService notifications, IDialogService dialogs, ThemeService theme)
    {
        _settings = settings;
        _notifications = notifications;
        _dialogs = dialogs;
        _theme = theme;
        NotifyOnFinish = settings.Current.NotifyOnFinish;
        NotifyWhenActive = settings.Current.NotifyWhenActive;
        Theme = settings.Current.Theme;
        _loaded = true;
    }

    private readonly bool _loaded;

    public sealed record ThemeOption(AppTheme Value, string Label);

    public IReadOnlyList<ThemeOption> ThemeOptions { get; } =
    [
        new(AppTheme.System, "Use Windows setting"),
        new(AppTheme.Light, "Light"),
        new(AppTheme.Dark, "Dark"),
    ];

    [ObservableProperty] public partial AppTheme Theme { get; set; }

    partial void OnThemeChanged(AppTheme value)
    {
        if (!_loaded) return;
        _settings.Current.Theme = value;
        _theme.Apply(value);
        Save();
    }

    [ObservableProperty] public partial bool NotifyOnFinish { get; set; }
    [ObservableProperty] public partial bool NotifyWhenActive { get; set; }

    /// <summary>Shown under the notification settings when sending or saving fails.</summary>
    [ObservableProperty] public partial string Message { get; set; } = "";

    partial void OnNotifyOnFinishChanged(bool value)
    {
        _settings.Current.NotifyOnFinish = value;
        Save();
    }

    partial void OnNotifyWhenActiveChanged(bool value)
    {
        _settings.Current.NotifyWhenActive = value;
        Save();
    }

    private void Save()
    {
        if (!_loaded) return;
        Message = _settings.Save() ? "" : "Couldn't save the settings. They apply until RoboUI is closed.";
    }

    [RelayCommand]
    private void SendTestNotification() =>
        Message = _notifications.ShowTest()
            ? ""
            : "Windows didn't accept the notification. Check that notifications are allowed in Windows settings.";

    [RelayCommand]
    private void OpenWindowsNotificationSettings() => _dialogs.OpenInShell("ms-settings:notifications");
}
