using YASS.Shared.Enums;

namespace YASS.Shared.Models;

/// <summary>
/// 流配置
/// </summary>
public class StreamConfig
{
    /// <summary>
    /// 视频编码格式
    /// </summary>
    public CodecType Codec { get; set; } = CodecType.H264;

    /// <summary>
    /// 编码器类型
    /// </summary>
    public EncoderType Encoder { get; set; } = EncoderType.Auto;

    /// <summary>
    /// 视频宽度
    /// </summary>
    public int Width { get; set; } = 1920;

    /// <summary>
    /// 视频高度
    /// </summary>
    public int Height { get; set; } = 1080;

    /// <summary>
    /// 帧率 (FPS)
    /// </summary>
    public int FrameRate { get; set; } = 30;

    /// <summary>
    /// 视频码率 (kbps)
    /// </summary>
    public int VideoBitrate { get; set; } = 4000;

    /// <summary>
    /// 关键帧间隔 (秒)
    /// </summary>
    public int KeyFrameInterval { get; set; } = 2;

    /// <summary>
    /// 编码预设 (ultrafast, superfast, veryfast, faster, fast, medium, slow, slower, veryslow)
    /// </summary>
    public string Preset { get; set; } = "fast";

    /// <summary>
    /// 是否启用音频
    /// </summary>
    public bool AudioEnabled { get; set; } = false;

    /// <summary>
    /// 音频码率 (kbps)
    /// </summary>
    public int AudioBitrate { get; set; } = 128;
}
