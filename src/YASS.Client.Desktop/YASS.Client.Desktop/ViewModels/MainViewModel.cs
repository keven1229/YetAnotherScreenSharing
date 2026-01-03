using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using YASS.Client.Core.Encoding;
using YASS.Client.Core.Interfaces;
using YASS.Client.Desktop.Services;
using YASS.Client.Desktop.Views;
using YASS.Shared.Enums;
using YASS.Shared.Models;

namespace YASS.Client.Desktop.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IApiService _apiService;
    private readonly IScreenCapture _screenCapture;

    [ObservableProperty]
    private string _title = "YASS - Yet Another Screen Sharing";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private ObservableCollection<CaptureSource> _captureSources = [];

    [ObservableProperty]
    private CaptureSource? _selectedCaptureSource;

    [ObservableProperty]
    private ObservableCollection<RoomInfo> _rooms = [];

    [ObservableProperty]
    private RoomInfo? _selectedRoom;

    [ObservableProperty]
    private ObservableCollection<EncoderCapability> _availableEncoders = [];

    [ObservableProperty]
    private StreamConfig _streamConfig = new();

    [ObservableProperty]
    private string _newRoomName = string.Empty;

    public MainViewModel(
        ISettingsService settingsService,
        IApiService apiService,
        IScreenCapture screenCapture)
    {
        _settingsService = settingsService;
        _apiService = apiService;
        _screenCapture = screenCapture;

        // 加载设置
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        IsLoading = true;
        StatusMessage = "正在初始化...";

        try
        {
            await _settingsService.LoadAsync();
            StreamConfig = _settingsService.Settings.DefaultStreamConfig;

            // 检测可用编码器
            var encoders = EncoderDetector.DetectAvailableEncoders();
            AvailableEncoders = new ObservableCollection<EncoderCapability>(encoders);

            // 获取捕获源
            await RefreshCaptureSourcesAsync();

            // 加载房间列表
            await RefreshRoomsAsync();

            StatusMessage = "就绪";
        }
        catch (Exception ex)
        {
            StatusMessage = $"初始化失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshCaptureSourcesAsync()
    {
        var sources = await _screenCapture.GetCaptureSourcesAsync();
        CaptureSources = new ObservableCollection<CaptureSource>(sources);
        
        // 默认选择主显示器
        SelectedCaptureSource = CaptureSources.FirstOrDefault(s => s.IsPrimary) 
            ?? CaptureSources.FirstOrDefault();
    }

    [RelayCommand]
    private async Task RefreshRoomsAsync()
    {
        try
        {
            var response = await _apiService.GetRoomsAsync();
            if (response != null)
            {
                Rooms = new ObservableCollection<RoomInfo>(response.Rooms);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"获取房间列表失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CreateRoomAsync()
    {
        if (string.IsNullOrWhiteSpace(NewRoomName))
        {
            StatusMessage = "请输入房间名称";
            return;
        }

        try
        {
            IsLoading = true;
            var response = await _apiService.CreateRoomAsync(new Shared.DTOs.CreateRoomRequest
            {
                Name = NewRoomName
            });

            if (response != null)
            {
                Rooms.Insert(0, response.Room);
                SelectedRoom = response.Room;
                NewRoomName = string.Empty;
                StatusMessage = $"房间 '{response.Room.Name}' 创建成功";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"创建房间失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        _settingsService.Settings.DefaultStreamConfig = StreamConfig;
        await _settingsService.SaveAsync();
        StatusMessage = "设置已保存";
    }

    [RelayCommand]
    private void OpenStreamingView()
    {
        if (SelectedCaptureSource == null)
        {
            StatusMessage = "请选择捕获源";
            return;
        }

        if (SelectedRoom == null)
        {
            StatusMessage = "请选择或创建房间";
            return;
        }

        // 创建推流视图
        var streamingViewModel = App.Services.GetRequiredService<StreamingViewModel>();
        streamingViewModel.Initialize(SelectedCaptureSource, SelectedRoom, StreamConfig);

        // 打开推流窗口
        var streamingPage = new StreamingPage(streamingViewModel);
        streamingPage.Owner = System.Windows.Application.Current.MainWindow;
        streamingPage.Show();
    }
}
