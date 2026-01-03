using Wpf.Ui.Controls;
using YASS.Client.Desktop.ViewModels;

namespace YASS.Client.Desktop.Views;

public partial class StreamingPage : FluentWindow
{
    public StreamingViewModel ViewModel { get; }

    public StreamingPage(StreamingViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        
        // 窗口加载后自动开始推流
        Loaded += async (s, e) =>
        {
            if (ViewModel.StartStreamingCommand.CanExecute(null))
            {
                await ViewModel.StartStreamingCommand.ExecuteAsync(null);
            }
        };
    }
}
