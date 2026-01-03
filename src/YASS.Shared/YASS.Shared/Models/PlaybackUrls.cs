using YASS.Shared.Enums;

namespace YASS.Shared.Models;

/// <summary>
/// 播放地址信息
/// </summary>
public class PlaybackUrls
{
    /// <summary>
    /// 房间ID
    /// </summary>
    public string RoomId { get; set; } = string.Empty;

    /// <summary>
    /// RTMP 播放地址 (H.265 原始流，客户端用)
    /// </summary>
    public string? RtmpUrl { get; set; }

    /// <summary>
    /// HTTP-FLV 播放地址 (H.265 原始流，客户端用)
    /// </summary>
    public string? HttpFlvUrl { get; set; }

    /// <summary>
    /// HTTP-FLV H.264 转码流地址 (Web 用)
    /// </summary>
    public string? HttpFlvH264Url { get; set; }

    /// <summary>
    /// HLS 播放地址
    /// </summary>
    public string? HlsUrl { get; set; }

    /// <summary>
    /// 原始流编码格式
    /// </summary>
    public CodecType OriginalCodec { get; set; }
}
