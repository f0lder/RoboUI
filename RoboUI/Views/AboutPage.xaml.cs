using System.Windows.Controls;
using RoboUI.ViewModels;

namespace RoboUI.Views;

public partial class AboutPage : Page
{
    public AboutPage(AboutViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
