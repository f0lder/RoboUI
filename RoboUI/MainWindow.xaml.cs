using System.ComponentModel;
using RoboUI.ViewModels;

namespace RoboUI;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly MainViewModel _viewModel;
    private bool _closeApproved;
    private bool _closePending;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
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
