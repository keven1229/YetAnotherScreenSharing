using Microsoft.Extensions.Logging;
using YASS.Client.Core.Interfaces;
using YASS.Shared.Models;
using YASS.Shared.Enums;

namespace YASS.Client.Core.Services;

/// <summary>
/// 屏幕共享服务 - 协调屏幕捕获、编码和推流
/// </summary>
public class ScreenSharingService : IDisposable
{
    private readonly ILogger<ScreenSharingService> _logger;
    private readonly IScreenCapture _screenCapture;
    private readonly IEncoder _encoder;
    private readonly IStreamer _streamer;
    private bool _isRunning;
    private CancellationTokenSource? _cts;

    public bool IsRunning => _isRunning;

    /// <summary>
    /// 状态变更事件
    /// </summary>
    public event EventHandler<SharingStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// 统计信息更新事件
    /// </summary>
    public event EventHandler<SharingStatsEventArgs>? StatsUpdated;

    public ScreenSharingService(
        ILogger<ScreenSharingService> logger,
        IScreenCapture screenCapture,
        IEncoder encoder,
        IStreamer streamer)
    {
        _logger = logger;
        _screenCapture = screenCapture;
        _encoder = encoder;
        _streamer = streamer;

        // 订阅事件
        _screenCapture.FrameArrived += OnFrameArrived;
        _screenCapture.CaptureStopped += OnCaptureStopped;
        _streamer.StateChanged += OnStreamerStateChanged;
        _streamer.StatsUpdated += OnStreamerStatsUpdated;
    }

    /// <summary>
    /// 开始屏幕共享
    /// </summary>
    public async Task StartAsync(SharingOptions options)
    {
        if (_isRunning)
        {
            throw new InvalidOperationException("Screen sharing is already running");
        }

        _logger.LogInformation("Starting screen sharing to {Url}", options.PublishUrl);
        _cts = new CancellationTokenSource();

        try
        {
            // 1. 初始化编码器
            await _encoder.InitializeAsync(new EncoderOptions
            {
                Width = options.StreamConfig.Width,
                Height = options.StreamConfig.Height,
                FrameRate = options.StreamConfig.FrameRate,
                Bitrate = options.StreamConfig.VideoBitrate,
                KeyFrameIntervalSeconds = options.StreamConfig.KeyFrameInterval,
                Preset = options.StreamConfig.Preset,
                EncoderType = options.StreamConfig.Encoder,
                CodecType = options.StreamConfig.Codec,
                InputFormat = PixelFormat.BGRA32
            });

            // 2. 连接推流服务器
            await _streamer.ConnectAsync(new StreamerOptions
            {
                Url = options.PublishUrl,
                Width = options.StreamConfig.Width,
                Height = options.StreamConfig.Height,
                FrameRate = options.StreamConfig.FrameRate,
                CodecType = options.StreamConfig.Codec
            });

            // 2.5 传递编码器的 extradata (SPS/PPS) 给推流器
            // 这对于 RTMP 推流至关重要，SRS 需要 extradata 来正确解码视频流
            if (_encoder is Encoding.FFmpegEncoder ffmpegEncoder)
            {
                var extraData = ffmpegEncoder.GetExtraData();
                if (extraData != null && extraData.Length > 0)
                {
                    if (_streamer is Streaming.RtmpStreamer rtmpStreamer)
                    {
                        rtmpStreamer.SetExtraData(extraData);
                        _logger.LogInformation("Passed encoder extradata ({Length} bytes) to RtmpStreamer", extraData.Length);
                    }
                    else if (_streamer is Streaming.FFmpegProcessStreamer processStreamer)
                    {
                        processStreamer.SetExtraData(extraData);
                        _logger.LogInformation("Passed encoder extradata ({Length} bytes) to FFmpegProcessStreamer", extraData.Length);
                    }
                    else if (_streamer is Streaming.SafeRtmpStreamer safeStreamer)
                    {
                        safeStreamer.SetExtraData(extraData);
                        _logger.LogInformation("Passed encoder extradata ({Length} bytes) to SafeRtmpStreamer", extraData.Length);
                    }
                }
                else
                {
                    _logger.LogWarning("Encoder extradata is empty - RTMP stream may fail");
                }
            }

            // 3. 开始屏幕捕获
            await _screenCapture.StartCaptureAsync(options.CaptureSource, new CaptureOptions
            {
                TargetFrameRate = options.StreamConfig.FrameRate,
                CaptureCursor = options.CaptureCursor,
                OutputWidth = options.StreamConfig.Width,
                OutputHeight = options.StreamConfig.Height
            });

            _isRunning = true;
            OnStateChanged(SharingState.Running, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start screen sharing");
            await StopAsync();
            OnStateChanged(SharingState.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// 停止屏幕共享
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isRunning) return;

        _logger.LogInformation("Stopping screen sharing");
        _cts?.Cancel();

        try
        {
            await _screenCapture.StopCaptureAsync();
            
            // 刷新编码器
            var remainingPackets = await _encoder.FlushAsync();
            foreach (var packet in remainingPackets)
            {
                await _streamer.SendPacketAsync(packet);
            }

            await _streamer.DisconnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during stop");
        }
        finally
        {
            _isRunning = false;
            OnStateChanged(SharingState.Stopped, null);
        }
    }

    private async void OnFrameArrived(object? sender, FrameArrivedEventArgs e)
    {
        if (!_isRunning || _cts?.IsCancellationRequested == true) return;

        // 检查推流器状态
        if (!_streamer.IsConnected)
        {
            return;
        }

        try
        {
            // 编码帧
            var packet = await _encoder.EncodeAsync(
                e.Data, e.Width, e.Height, e.Stride, e.Format, e.Timestamp);

            if (packet != null)
            {
                // 发送到服务器
                await _streamer.SendPacketAsync(packet);
            }
        }
        catch (InvalidOperationException)
        {
            // 推流器已断开，忽略
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing frame");
        }
    }

    private void OnCaptureStopped(object? sender, CaptureStoppedEventArgs e)
    {
        if (e.Reason == CaptureStopReason.Error)
        {
            _logger.LogError("Capture stopped due to error: {Error}", e.Error);
            OnStateChanged(SharingState.Error, e.Error);
        }
    }

    private void OnStreamerStateChanged(object? sender, StreamerStateChangedEventArgs e)
    {
        _logger.LogInformation("Streamer state changed: {OldState} -> {NewState}", e.OldState, e.NewState);

        if (e.NewState == StreamerState.Error)
        {
            OnStateChanged(SharingState.Error, e.Error);
        }
    }

    private void OnStreamerStatsUpdated(object? sender, StreamerStatsEventArgs e)
    {
        StatsUpdated?.Invoke(this, new SharingStatsEventArgs
        {
            BytesSent = e.BytesSent,
            CurrentBitrate = e.CurrentBitrate,
            FramesSent = e.FramesSent,
            FramesDropped = e.FramesDropped,
            DurationSeconds = e.DurationSeconds,
            CurrentFps = e.CurrentFps,
            EncoderType = _encoder.CurrentEncoderType,
            CodecType = _encoder.CurrentCodecType
        });
    }

    private void OnStateChanged(SharingState state, string? error)
    {
        StateChanged?.Invoke(this, new SharingStateChangedEventArgs
        {
            State = state,
            Error = error
        });
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        
        _screenCapture.FrameArrived -= OnFrameArrived;
        _screenCapture.CaptureStopped -= OnCaptureStopped;
        _streamer.StateChanged -= OnStreamerStateChanged;
        _streamer.StatsUpdated -= OnStreamerStatsUpdated;
        
        _screenCapture.Dispose();
        _encoder.Dispose();
        _streamer.Dispose();
        _cts?.Dispose();
        
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 屏幕共享选项
/// </summary>
public class SharingOptions
{
    /// <summary>
    /// 捕获源
    /// </summary>
    public required CaptureSource CaptureSource { get; init; }

    /// <summary>
    /// 推流 URL
    /// </summary>
    public required string PublishUrl { get; init; }

    /// <summary>
    /// 流配置
    /// </summary>
    public required StreamConfig StreamConfig { get; init; }

    /// <summary>
    /// 是否捕获鼠标
    /// </summary>
    public bool CaptureCursor { get; init; } = true;
}

/// <summary>
/// 共享状态
/// </summary>
public enum SharingState
{
    Stopped,
    Starting,
    Running,
    Stopping,
    Error
}

/// <summary>
/// 状态变更事件参数
/// </summary>
public class SharingStateChangedEventArgs : EventArgs
{
    public SharingState State { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// 统计信息事件参数
/// </summary>
public class SharingStatsEventArgs : EventArgs
{
    public long BytesSent { get; init; }
    public double CurrentBitrate { get; init; }
    public long FramesSent { get; init; }
    public long FramesDropped { get; init; }
    public double DurationSeconds { get; init; }
    public double CurrentFps { get; init; }
    public EncoderType EncoderType { get; init; }
    public CodecType CodecType { get; init; }
}
