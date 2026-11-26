using RoboUI.Core.Settings;

namespace RoboUI.Core.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "RoboUI.SettingsTests." + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_dir, "settings.json");

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void Missing_file_gives_defaults()
    {
        var settings = new SettingsStore(FilePath).Load();

        Assert.True(settings.NotifyOnFinish);
        Assert.False(settings.NotifyWhenActive);
        Assert.Equal(AppTheme.System, settings.Theme);
    }

    [Fact]
    public void Theme_is_saved_by_name()
    {
        new SettingsStore(FilePath).Save(new AppSettings { Theme = AppTheme.Light });

        Assert.Contains("\"Light\"", File.ReadAllText(FilePath));
        Assert.Equal(AppTheme.Light, new SettingsStore(FilePath).Load().Theme);
    }

    [Fact]
    public void Unknown_theme_name_gives_defaults_instead_of_failing()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, """{ "Theme": "Purple" }""");

        Assert.Equal(AppTheme.System, new SettingsStore(FilePath).Load().Theme);
    }

    [Fact]
    public void Saved_settings_round_trip()
    {
        var store = new SettingsStore(FilePath);
        store.Save(new AppSettings { NotifyOnFinish = false, NotifyWhenActive = true });

        var loaded = new SettingsStore(FilePath).Load();

        Assert.False(loaded.NotifyOnFinish);
        Assert.True(loaded.NotifyWhenActive);
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public void Properties_missing_from_the_file_keep_their_defaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, """{ "NotifyWhenActive": true }""");

        var loaded = new SettingsStore(FilePath).Load();

        Assert.True(loaded.NotifyOnFinish);
        Assert.True(loaded.NotifyWhenActive);
    }

    [Fact]
    public void Corrupt_file_is_set_aside_and_gives_defaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ not json");

        var loaded = new SettingsStore(FilePath).Load();

        Assert.True(loaded.NotifyOnFinish);
        Assert.False(File.Exists(FilePath));
        Assert.True(File.Exists(FilePath + ".bad"));
    }
}
