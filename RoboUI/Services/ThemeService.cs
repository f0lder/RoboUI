using System.Windows;
using RoboUI.Core.Settings;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace RoboUI.Services;

/// <summary>
/// Applies the light or dark theme. With <see cref="AppTheme.System"/> it follows the Windows
/// setting, including the accent colour, and switches live when Windows does.
/// </summary>
public sealed class ThemeService
{
    private Window? _watched;

    /// <summary>Applies <paramref name="theme"/> to the app. Call on the UI thread, once the main window exists.</summary>
    public void Apply(AppTheme theme)
    {
        var window = Application.Current?.MainWindow;

        if (theme == AppTheme.System)
        {
            ApplicationThemeManager.ApplySystemTheme(updateAccent: true);
            if (window is not null && _watched != window)
            {
                SystemThemeWatcher.Watch(window, WindowBackdropType.Mica, updateAccents: true);
                _watched = window;
            }
            return;
        }

        if (_watched is not null)
        {
            SystemThemeWatcher.UnWatch(_watched);
            _watched = null;
        }

        var applicationTheme = theme == AppTheme.Light ? ApplicationTheme.Light : ApplicationTheme.Dark;
        ApplicationThemeManager.Apply(applicationTheme, WindowBackdropType.Mica, updateAccent: true);
    }
}
