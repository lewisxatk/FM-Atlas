using System.Windows;
using FMAtlas.Desktop.ViewModels;

namespace FMAtlas.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
