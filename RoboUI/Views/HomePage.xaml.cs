using System.Windows.Controls;
using RoboUI.ViewModels;

namespace RoboUI.Views;

/// <summary>The copy job screen.</summary>
public partial class HomePage : Page
{
    public HomePage(MainViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
