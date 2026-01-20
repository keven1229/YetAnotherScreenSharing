using Wpf.Ui.Controls;
using YASS.Client.Desktop.ViewModels;

namespace YASS.Client.Desktop;

public partial class MainWindow : FluentWindow
{
    public MainViewModel ViewModel { get; }

    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }
}