namespace YASS.Shared.Enums;

/// <summary>
/// 视频编码器类型
/// </summary>
public enum EncoderType
{
    /// <summary>
    /// 自动检测最佳可用编码器
    /// </summary>
    Auto = 0,

    /// <summary>
    /// NVIDIA NVENC 硬件编码
    /// </summary>
    NVENC = 1,

    /// <summary>
    /// AMD AMF 硬件编码
    /// </summary>
    AMF = 2,

    /// <summary>
    /// Intel Quick Sync Video 硬件编码
    /// </summary>
    QSV = 3,

    /// <summary>
    /// CPU 软件编码 (libx264/libx265)
    /// </summary>
    Software = 4
}
