using System.Windows.Controls;
using RoboUI.ViewModels;

namespace RoboUI.Views;

public partial class SettingsPage : Page
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
