using System.IO;
using RoboUI.Core.Settings;

namespace RoboUI.Services;

/// <summary>The app's settings, loaded once at startup. Call <see cref="Save"/> after changing <see cref="Current"/>.</summary>
public sealed class SettingsService(AppPaths paths)
{
    private readonly SettingsStore _store = new(paths.SettingsFile);

    public AppSettings Current { get; } = LoadOrDefaults(paths.SettingsFile);

    private static AppSettings LoadOrDefaults(string file)
    {
        try
        {
            return new SettingsStore(file).Load();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    /// <summary>Saves the settings. Returns false if the file couldn't be written; the settings still apply until exit.</summary>
    public bool Save()
    {
        try
        {
            _store.Save(Current);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
