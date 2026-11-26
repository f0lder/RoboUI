using System.ComponentModel;
using System.Windows.Shell;
using RoboUI.ViewModels;
using RoboUI.Views;

namespace RoboUI;

/// <summary>The shell: title bar and sidebar. Pages come from the DI container and are singletons, so they keep their state.</summary>
public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly MainViewModel _viewModel;
    private bool _closeApproved;
    private bool _closePending;

    public MainWindow(MainViewModel viewModel, HistoryViewModel history, IServiceProvider services)
    {
        _viewModel = viewModel;
        InitializeComponent();
        RootNavigation.SetServiceProvider(services);
        Loaded += (_, _) => RootNavigation.Navigate(typeof(HomePage));
        history.ShowHomeRequested += () => RootNavigation.Navigate(typeof(HomePage));

        TaskbarItemInfo = new TaskbarItemInfo();
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Activated += (_, _) => _viewModel.AcknowledgeTaskbarProgress();
    }

    /// <summary>Mirrors the run's progress on the taskbar button.</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.TaskbarState):
                TaskbarItemInfo.ProgressState = _viewModel.TaskbarState;
                break;
            case nameof(MainViewModel.TaskbarValue):
                TaskbarItemInfo.ProgressValue = _viewModel.TaskbarValue;
                break;
            case nameof(MainViewModel.IsRunning) when !_viewModel.IsRunning && IsActive:
                // The user is looking at the result already; no need to keep it on the taskbar.
                _viewModel.AcknowledgeTaskbarProgress();
                break;
        }
    }

    /// <summary>While a copy runs: confirm, stop robocopy, wait for it to exit, then close.</summary>
    protected override async void OnClosing(CancelEventArgs e)
    {
        if (_closeApproved || !_viewModel.IsRunning)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        if (_closePending) return;
        _closePending = true;
        try
        {
            if (!await _viewModel.ConfirmStopForExitAsync()) return;
            await _viewModel.StopAndWaitAsync();
            _closeApproved = true;
            Close();
        }
        finally
        {
            _closePending = false;
        }
    }
}
