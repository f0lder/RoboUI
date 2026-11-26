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
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();
        _host.Start();

        var window = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Normally released when the run ends; this covers any path that skipped it.
        _host?.Services.GetService<IKeepAwakeService>()?.Release();
        _host?.Dispose();
        base.OnExit(e);
    }
}
