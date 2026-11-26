using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RoboUI.Core.Robocopy;
using RoboUI.Services;
using RoboUI.ViewModels;

namespace RoboUI;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = Host.CreateApplicationBuilder(e.Args);
        builder.Services.AddSingleton<AppPaths>();
        builder.Services.AddSingleton(new RobocopyRunner());
        builder.Services.AddSingleton<IDialogService, DialogService>();
        builder.Services.AddSingleton<IKeepAwakeService, KeepAwakeService>();
        builder.Services.AddSingleton<RunHistoryService>();
        builder.Services.AddSingleton<SettingsService>();
        builder.Services.AddSingleton<NotificationService>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<HistoryViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddSingleton<AboutViewModel>();
        builder.Services.AddSingleton<Views.HomePage>();
        builder.Services.AddSingleton<Views.HistoryPage>();
        builder.Services.AddSingleton<Views.SettingsPage>();
        builder.Services.AddSingleton<Views.AboutPage>();
        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();
        _host.Start();

        // Created up front so it is listening when the first run finishes.
        _host.Services.GetRequiredService<NotificationService>();

        var window = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();

        // Read the saved history in the background; the History page fills in when it is done.
        _ = _host.Services.GetRequiredService<HistoryViewModel>().LoadAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Normally released when the run ends; this covers any path that skipped it.
        _host?.Services.GetService<IKeepAwakeService>()?.Release();
        _host?.Dispose();
        base.OnExit(e);
    }
}
