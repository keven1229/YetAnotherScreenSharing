using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using YASS.Client.Core.Interfaces;
using YASS.Client.Core.Services;
using YASS.Client.Desktop.Services;
using YASS.Shared.Enums;
using YASS.Shared.Models;

namespace YASS.Client.Desktop.ViewModels;

public partial class StreamingViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<StreamingViewModel> _logger;
    private readonly IApiService _apiService;
    private readonly ISettingsService _settingsService;
    private readonly ScreenSharingService _sharingService;
    private CaptureSource? _captureSource;
    private RoomInfo? _room;
    private StreamConfig? _streamConfig;

    [ObservableProperty]
    private bool _isStreaming;

    [ObservableProperty]
    private SharingState _state = SharingState.Stopped;

    [ObservableProperty]
    private string _statusMessage = "准备就绪";

    [ObservableProperty]
    private string _duration = "00:00:00";

    [ObservableProperty]
    private double _currentBitrate;

    [ObservableProperty]
    private double _currentFps;

    [ObservableProperty]
    private long _framesSent;

    [ObservableProperty]
    private long _framesDropped;

    [ObservableProperty]
    private string _encoderInfo = string.Empty;

    [ObservableProperty]
    private string _roomName = string.Empty;

    [ObservableProperty]
    private string _captureSourceName = string.Empty;

    public StreamingViewModel(
        ILogger<StreamingViewModel> logger,
        IApiService apiService,
        ISettingsService settingsService,
        ScreenSharingService sharingService)
    {
        _logger = logger;
        _apiService = apiService;
        _settingsService = settingsService;
        _sharingService = sharingService;

        _sharingService.StateChanged += OnStateChanged;
        _sharingService.StatsUpdated += OnStatsUpdated;
    }

    public void Initialize(CaptureSource captureSource, RoomInfo room, StreamConfig streamConfig)
    {
        _captureSource = captureSource;
        _room = room;
        _streamConfig = streamConfig;

        RoomName = room.Name;
        CaptureSourceName = captureSource.DisplayName;
    }

    [RelayCommand]
    private async Task StartStreamingAsync()
    {
        if (_captureSource == null || _room == null || _streamConfig == null)
        {
            StatusMessage = "请先配置推流参数";
            return;
        }

        try
        {
            IsStreaming = true;
            StatusMessage = "正在获取推流地址...";

            // 获取推流凭证
            var credentials = await _apiService.GetPublishCredentialsAsync(_room.Id);
            if (credentials == null)
            {
                StatusMessage = "获取推流凭证失败";
                IsStreaming = false;
                return;
            }

            StatusMessage = "正在连接服务器...";

            await _sharingService.StartAsync(new SharingOptions
            {
                CaptureSource = _captureSource,
                PublishUrl = credentials.FullPublishUrl,
                StreamConfig = _streamConfig,
                CaptureCursor = _settingsService.Settings.CaptureCursor
            });

            StatusMessage = "正在推流";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start streaming");
            StatusMessage = $"推流失败: {ex.Message}";
            IsStreaming = false;
        }
    }

    [RelayCommand]
    private async Task StopStreamingAsync()
    {
        try
        {
            StatusMessage = "正在停止推流...";
            await _sharingService.StopAsync();
            StatusMessage = "推流已停止";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping streaming");
            StatusMessage = $"停止失败: {ex.Message}";
        }
        finally
        {
            IsStreaming = false;
        }
    }

    private void OnStateChanged(object? sender, SharingStateChangedEventArgs e)
    {
        State = e.State;

        switch (e.State)
        {
            case SharingState.Running:
                StatusMessage = "正在推流";
                break;
            case SharingState.Stopped:
                StatusMessage = "推流已停止";
                IsStreaming = false;
                break;
            case SharingState.Error:
                StatusMessage = $"错误: {e.Error}";
                IsStreaming = false;
                break;
        }
    }

    private void OnStatsUpdated(object? sender, SharingStatsEventArgs e)
    {
        CurrentBitrate = e.CurrentBitrate;
        CurrentFps = e.CurrentFps;
        FramesSent = e.FramesSent;
        FramesDropped = e.FramesDropped;
        Duration = TimeSpan.FromSeconds(e.DurationSeconds).ToString(@"hh\:mm\:ss");
        EncoderInfo = $"{e.EncoderType} / {e.CodecType}";
    }

    public void Dispose()
    {
        _sharingService.StateChanged -= OnStateChanged;
        _sharingService.StatsUpdated -= OnStatsUpdated;
        GC.SuppressFinalize(this);
    }
}
