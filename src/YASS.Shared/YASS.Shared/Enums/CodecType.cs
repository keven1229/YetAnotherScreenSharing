namespace YASS.Shared.Enums;

/// <summary>
/// 视频编码格式
/// </summary>
public enum CodecType
{
    /// <summary>
    /// H.264/AVC - 兼容性最好
    /// </summary>
    H264 = 0,

    /// <summary>
    /// H.265/HEVC - 高压缩效率
    /// </summary>
    H265 = 1,

    /// <summary>
    /// AV1 - 开源最高效（编码较慢）
    /// </summary>
    AV1 = 2
}
