using System.Windows.Controls;
using RoboUI.ViewModels;

namespace RoboUI.Views;

/// <summary>Past runs, newest first.</summary>
public partial class HistoryPage : Page
{
    public HistoryPage(HistoryViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
