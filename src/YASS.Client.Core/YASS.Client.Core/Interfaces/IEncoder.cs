using YASS.Shared.Enums;

namespace YASS.Client.Core.Interfaces;

/// <summary>
/// 视频编码器接口
/// </summary>
public interface IEncoder : IDisposable
{
    /// <summary>
    /// 初始化编码器
    /// </summary>
    Task InitializeAsync(EncoderOptions options);

    /// <summary>
    /// 编码帧
    /// </summary>
    /// <param name="frameData">帧数据指针</param>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <param name="stride">行字节数</param>
    /// <param name="format">像素格式</param>
    /// <param name="timestamp">时间戳 (毫秒)</param>
    /// <returns>编码后的数据包</returns>
    Task<EncodedPacket?> EncodeAsync(nint frameData, int width, int height, int stride, PixelFormat format, long timestamp);

    /// <summary>
    /// 刷新编码器缓冲区
    /// </summary>
    Task<IReadOnlyList<EncodedPacket>> FlushAsync();

    /// <summary>
    /// 是否已初始化
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// 当前使用的编码器类型
    /// </summary>
    EncoderType CurrentEncoderType { get; }

    /// <summary>
    /// 当前编码格式
    /// </summary>
    CodecType CurrentCodecType { get; }
}

/// <summary>
/// 编码器选项
/// </summary>
public class EncoderOptions
{
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
    /// 视频码率 (kbps)
    /// </summary>
    public int Bitrate { get; set; } = 4000;

    /// <summary>
    /// 关键帧间隔 (秒)
    /// </summary>
    public int KeyFrameIntervalSeconds { get; set; } = 2;

    /// <summary>
    /// 编码预设
    /// </summary>
    public string Preset { get; set; } = "fast";

    /// <summary>
    /// 编码器类型
    /// </summary>
    public EncoderType EncoderType { get; set; } = EncoderType.Auto;

    /// <summary>
    /// 编码格式
    /// </summary>
    public CodecType CodecType { get; set; } = CodecType.H265;

    /// <summary>
    /// 输入像素格式
    /// </summary>
    public PixelFormat InputFormat { get; set; } = PixelFormat.BGRA32;
}

/// <summary>
/// 编码后的数据包
/// </summary>
public class EncodedPacket : IDisposable
{
    /// <summary>
    /// 数据
    /// </summary>
    public byte[] Data { get; init; } = [];

    /// <summary>
    /// 解码时间戳
    /// </summary>
    public long Dts { get; init; }

    /// <summary>
    /// 显示时间戳
    /// </summary>
    public long Pts { get; init; }

    /// <summary>
    /// 是否为关键帧
    /// </summary>
    public bool IsKeyFrame { get; init; }

    /// <summary>
    /// 时间基准分子
    /// </summary>
    public int TimeBaseNum { get; init; }

    /// <summary>
    /// 时间基准分母
    /// </summary>
    public int TimeBaseDen { get; init; }

    public void Dispose()
    {
        // Data is managed, no need to dispose
    }
}
