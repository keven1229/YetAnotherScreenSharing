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
        
        // 窗口加载后自动获取推流地址
        Loaded += async (s, e) =>
        {
            if (ViewModel.LoadRtmpUrlCommand.CanExecute(null))
            {
                await ViewModel.LoadRtmpUrlCommand.ExecuteAsync(null);
            }
        };
    }
}
