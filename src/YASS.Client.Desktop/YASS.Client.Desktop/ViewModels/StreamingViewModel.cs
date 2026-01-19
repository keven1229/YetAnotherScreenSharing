using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using YASS.Client.Desktop.Services;
using YASS.Shared.Models;

namespace YASS.Client.Desktop.ViewModels;

public partial class StreamingViewModel : ObservableObject
{
    private readonly ILogger<StreamingViewModel> _logger;
    private readonly IApiService _apiService;
    private RoomInfo? _room;

    [ObservableProperty]
    private string _statusMessage = "准备就绪";

    [ObservableProperty]
    private string _roomName = string.Empty;

    [ObservableProperty]
    private string _rtmpUrl = string.Empty;

    [ObservableProperty]
    private string _streamKey = string.Empty;

    [ObservableProperty]
    private bool _isRtmpUrlReady;

    public StreamingViewModel(
        ILogger<StreamingViewModel> logger,
        IApiService apiService)
    {
        _logger = logger;
        _apiService = apiService;
    }

    public void Initialize(RoomInfo room)
    {
        _room = room;
        RoomName = room.Name;
        
        // 立即获取RTMP推流地址
        _ = LoadRtmpUrlAsync();
    }

    [RelayCommand]
    private async Task LoadRtmpUrlAsync()
    {
        if (_room == null)
        {
            StatusMessage = "房间信息无效";
            return;
        }

        try
        {
            StatusMessage = "正在获取推流地址...";

            // 获取推流凭证
            var credentials = await _apiService.GetPublishCredentialsAsync(_room.Id);
            if (credentials == null)
            {
                StatusMessage = "获取推流凭证失败";
                return;
            }

            // 解析RTMP地址和流密钥
            var fullUrl = credentials.FullPublishUrl;
            var lastSlashIndex = fullUrl.LastIndexOf('/');
            if (lastSlashIndex > 0)
            {
                RtmpUrl = fullUrl.Substring(0, lastSlashIndex);
                StreamKey = fullUrl.Substring(lastSlashIndex + 1);
            }
            else
            {
                RtmpUrl = fullUrl;
                StreamKey = "";
            }

            IsRtmpUrlReady = true;
            StatusMessage = "推流地址已生成，请使用OBS、FFmpeg等工具推流";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load RTMP URL");
            StatusMessage = $"获取推流地址失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CopyRtmpUrl()
    {
        if (string.IsNullOrEmpty(RtmpUrl))
        {
            StatusMessage = "推流地址为空";
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(RtmpUrl);
            StatusMessage = "推流地址已复制到剪贴板";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy RTMP URL");
            StatusMessage = "复制失败";
        }
    }

    [RelayCommand]
    private void CopyStreamKey()
    {
        if (string.IsNullOrEmpty(StreamKey))
        {
            StatusMessage = "流密钥为空";
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(StreamKey);
            StatusMessage = "流密钥已复制到剪贴板";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy stream key");
            StatusMessage = "复制失败";
        }
    }

    [RelayCommand]
    private void CopyFullUrl()
    {
        if (string.IsNullOrEmpty(RtmpUrl) || string.IsNullOrEmpty(StreamKey))
        {
            StatusMessage = "推流信息不完整";
            return;
        }

        try
        {
            var fullUrl = $"{RtmpUrl}/{StreamKey}";
            System.Windows.Clipboard.SetText(fullUrl);
            StatusMessage = "完整推流地址已复制到剪贴板";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy full URL");
            StatusMessage = "复制失败";
        }
    }
}
