using YASS.Shared.Enums;

namespace YASS.Client.Core.Interfaces;

/// <summary>
/// 流推送器接口
/// </summary>
public interface IStreamer : IDisposable
{
    /// <summary>
    /// 连接到服务器
    /// </summary>
    Task ConnectAsync(StreamerOptions options);

    /// <summary>
    /// 断开连接
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// 发送编码后的数据包
    /// </summary>
    Task SendPacketAsync(EncodedPacket packet);

    /// <summary>
    /// 是否已连接
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 连接状态变更事件
    /// </summary>
    event EventHandler<StreamerStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// 统计信息更新事件
    /// </summary>
    event EventHandler<StreamerStatsEventArgs>? StatsUpdated;
}

/// <summary>
/// 推流选项
/// </summary>
public class StreamerOptions
{
    /// <summary>
    /// 推流URL (rtmp://...)
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 视频宽度
    /// </summary>
    public int Width { get; set; } = 1920;

    /// <summary>
    /// 视频高度
    /// </summary>
    public int Height { get; set; } = 1080;

    /// <summary>
    /// 帧率
    /// </summary>
    public int FrameRate { get; set; } = 30;

    /// <summary>
    /// 视频编码格式
    /// </summary>
    public CodecType CodecType { get; set; } = CodecType.H265;

    /// <summary>
    /// 连接超时 (秒)
    /// </summary>
    public int ConnectTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// 重连次数
    /// </summary>
    public int ReconnectAttempts { get; set; } = 3;

    /// <summary>
    /// 重连间隔 (秒)
    /// </summary>
    public int ReconnectIntervalSeconds { get; set; } = 5;
}

/// <summary>
/// 推流状态
/// </summary>
public enum StreamerState
{
    /// <summary>
    /// 未连接
    /// </summary>
    Disconnected,

    /// <summary>
    /// 连接中
    /// </summary>
    Connecting,

    /// <summary>
    /// 已连接
    /// </summary>
    Connected,

    /// <summary>
    /// 推流中
    /// </summary>
    Streaming,

    /// <summary>
    /// 重连中
    /// </summary>
    Reconnecting,

    /// <summary>
    /// 发生错误
    /// </summary>
    Error
}

/// <summary>
/// 状态变更事件参数
/// </summary>
public class StreamerStateChangedEventArgs : EventArgs
{
    public StreamerState OldState { get; init; }
    public StreamerState NewState { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// 推流统计信息
/// </summary>
public class StreamerStatsEventArgs : EventArgs
{
    /// <summary>
    /// 已发送字节数
    /// </summary>
    public long BytesSent { get; init; }

    /// <summary>
    /// 当前比特率 (kbps)
    /// </summary>
    public double CurrentBitrate { get; init; }

    /// <summary>
    /// 已发送帧数
    /// </summary>
    public long FramesSent { get; init; }

    /// <summary>
    /// 丢弃帧数
    /// </summary>
    public long FramesDropped { get; init; }

    /// <summary>
    /// 推流时长 (秒)
    /// </summary>
    public double DurationSeconds { get; init; }

    /// <summary>
    /// 当前 FPS
    /// </summary>
    public double CurrentFps { get; init; }
}
