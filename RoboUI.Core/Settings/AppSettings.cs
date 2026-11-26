namespace RoboUI.Core.Settings;

/// <summary>User preferences. New properties need a default, because older settings files won't have them.</summary>
public sealed class AppSettings
{
    /// <summary>Show a Windows notification when a copy or test run finishes.</summary>
    public bool NotifyOnFinish { get; set; } = true;

    /// <summary>Notify even when RoboUI is the active window (normally only when it's in the background).</summary>
    public bool NotifyWhenActive { get; set; }

    /// <summary>Light, dark, or follow the Windows setting.</summary>
    public AppTheme Theme { get; set; } = AppTheme.System;
}

public enum AppTheme
{
    System,
    Light,
    Dark,
}
