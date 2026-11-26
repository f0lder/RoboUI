using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
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

        _thumbStart = new ThumbButtonInfo { ImageSource = PlayIcon(), Description = viewModel.StartButtonText };
        _thumbStart.Click += (_, _) => StartFromThumbnail();
        _thumbStop = new ThumbButtonInfo { ImageSource = StopIcon(), Description = "Stop", Command = viewModel.StopCommand };
        TaskbarItemInfo = new TaskbarItemInfo { ThumbButtonInfos = { _thumbStart, _thumbStop } };
        UpdateThumbButtons();

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Activated += (_, _) => _viewModel.AcknowledgeTaskbarProgress();
    }

    private readonly ThumbButtonInfo _thumbStart;
    private readonly ThumbButtonInfo _thumbStop;

    private void UpdateThumbButtons()
    {
        _thumbStart.IsEnabled = !_viewModel.IsRunning;
        _thumbStart.Description = _viewModel.StartButtonText;
        _thumbStop.IsEnabled = _viewModel.IsRunning;
    }

    /// <summary>
    /// Start from the taskbar thumbnail. The window comes to the front first, because a validation
    /// message or the Mirror/Move confirmation may need the user's attention.
    /// </summary>
    private void StartFromThumbnail()
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        RootNavigation.Navigate(typeof(HomePage));
        if (_viewModel.StartCommand.CanExecute(null)) _viewModel.StartCommand.Execute(null);
    }

    // Thumbnail button icons, drawn on a 16Ã—16 canvas. Coloured so they read on light and dark taskbars.
    private static DrawingImage PlayIcon() =>
        ThumbIcon(Geometry.Parse("M5,2.5 L13.5,8 L5,13.5 Z"), Color.FromRgb(0x3F, 0xB9, 0x50));

    private static DrawingImage StopIcon() =>
        ThumbIcon(new RectangleGeometry(new Rect(3, 3, 10, 10), 1.5, 1.5), Color.FromRgb(0xE5, 0x48, 0x4D));

    private static DrawingImage ThumbIcon(Geometry shape, Color color)
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, 16, 16))));
        group.Children.Add(new GeometryDrawing(new SolidColorBrush(color), null, shape));
        var image = new DrawingImage(group);
        image.Freeze();
        return image;
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
            case nameof(MainViewModel.IsRunning):
                UpdateThumbButtons();
                // The user is looking at the result already; no need to keep it on the taskbar.
                if (!_viewModel.IsRunning && IsActive) _viewModel.AcknowledgeTaskbarProgress();
                break;
            case nameof(MainViewModel.StartButtonText):
                UpdateThumbButtons();
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
