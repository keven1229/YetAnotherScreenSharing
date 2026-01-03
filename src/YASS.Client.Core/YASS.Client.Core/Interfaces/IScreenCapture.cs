namespace YASS.Client.Core.Interfaces;

/// <summary>
/// 屏幕捕获接口
/// </summary>
public interface IScreenCapture : IDisposable
{
    /// <summary>
    /// 获取可用的捕获源列表
    /// </summary>
    Task<IReadOnlyList<CaptureSource>> GetCaptureSourcesAsync();

    /// <summary>
    /// 开始捕获
    /// </summary>
    /// <param name="source">捕获源</param>
    /// <param name="options">捕获选项</param>
    Task StartCaptureAsync(CaptureSource source, CaptureOptions options);

    /// <summary>
    /// 停止捕获
    /// </summary>
    Task StopCaptureAsync();

    /// <summary>
    /// 是否正在捕获
    /// </summary>
    bool IsCapturing { get; }

    /// <summary>
    /// 帧到达事件
    /// </summary>
    event EventHandler<FrameArrivedEventArgs>? FrameArrived;

    /// <summary>
    /// 捕获停止事件
    /// </summary>
    event EventHandler<CaptureStoppedEventArgs>? CaptureStopped;
}

/// <summary>
/// 捕获源类型
/// </summary>
public enum CaptureSourceType
{
    /// <summary>
    /// 显示器
    /// </summary>
    Monitor,

    /// <summary>
    /// 窗口
    /// </summary>
    Window
}

/// <summary>
/// 捕获源
/// </summary>
public class CaptureSource
{
    /// <summary>
    /// 源ID
    /// </summary>
    public nint Handle { get; init; }

    /// <summary>
    /// 源类型
    /// </summary>
    public CaptureSourceType Type { get; init; }

    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 宽度
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// 高度
    /// </summary>
    public int Height { get; init; }

    /// <summary>
    /// 是否为主显示器
    /// </summary>
    public bool IsPrimary { get; init; }
}

/// <summary>
/// 捕获选项
/// </summary>
public class CaptureOptions
{
    /// <summary>
    /// 目标帧率
    /// </summary>
    public int TargetFrameRate { get; set; } = 30;

    /// <summary>
    /// 是否捕获鼠标光标
    /// </summary>
    public bool CaptureCursor { get; set; } = true;

    /// <summary>
    /// 输出宽度 (0 = 原始尺寸)
    /// </summary>
    public int OutputWidth { get; set; } = 0;

    /// <summary>
    /// 输出高度 (0 = 原始尺寸)
    /// </summary>
    public int OutputHeight { get; set; } = 0;
}

/// <summary>
/// 帧数据
/// </summary>
public class FrameArrivedEventArgs : EventArgs
{
    /// <summary>
    /// 帧数据指针
    /// </summary>
    public nint Data { get; init; }

    /// <summary>
    /// 数据大小
    /// </summary>
    public int Size { get; init; }

    /// <summary>
    /// 帧宽度
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// 帧高度
    /// </summary>
    public int Height { get; init; }

    /// <summary>
    /// 行字节数
    /// </summary>
    public int Stride { get; init; }

    /// <summary>
    /// 像素格式
    /// </summary>
    public PixelFormat Format { get; init; }

    /// <summary>
    /// 时间戳 (毫秒)
    /// </summary>
    public long Timestamp { get; init; }
}

/// <summary>
/// 像素格式
/// </summary>
public enum PixelFormat
{
    BGRA32,
    RGBA32,
    RGB24,
    BGR24,
    NV12,
    YUV420P
}

/// <summary>
/// 捕获停止事件参数
/// </summary>
public class CaptureStoppedEventArgs : EventArgs
{
    /// <summary>
    /// 停止原因
    /// </summary>
    public CaptureStopReason Reason { get; init; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? Error { get; init; }
}

/// <summary>
/// 捕获停止原因
/// </summary>
public enum CaptureStopReason
{
    /// <summary>
    /// 用户请求停止
    /// </summary>
    UserRequested,

    /// <summary>
    /// 源已关闭
    /// </summary>
    SourceClosed,

    /// <summary>
    /// 发生错误
    /// </summary>
    Error
}
