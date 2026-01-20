namespace YASS.Shared.Models;

/// <summary>
/// 推流凭证
/// </summary>
public class PublishCredentials
{
    /// <summary>
    /// 房间ID
    /// </summary>
    public string RoomId { get; set; } = string.Empty;

    /// <summary>
    /// 推流密钥
    /// </summary>
    public string StreamKey { get; set; } = string.Empty;

    /// <summary>
    /// RTMP 推流地址
    /// </summary>
    public string RtmpUrl { get; set; } = string.Empty;

    /// <summary>
    /// 完整的推流URL（包含密钥）
    /// </summary>
    public string FullPublishUrl { get; set; } = string.Empty;

    /// <summary>
    /// 凭证过期时间
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}
