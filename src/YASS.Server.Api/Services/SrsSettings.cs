namespace YASS.Server.Api.Services;

/// <summary>
/// SRS 服务器配置
/// </summary>
public class SrsSettings
{
    /// <summary>
    /// RTMP 服务器地址
    /// </summary>
    public string RtmpServer { get; set; } = "rtmp://localhost:1935";

    /// <summary>
    /// HTTP-FLV 服务器地址
    /// </summary>
    public string HttpFlvServer { get; set; } = "http://localhost:8080";

    /// <summary>
    /// HLS 服务器地址
    /// </summary>
    public string HlsServer { get; set; } = "http://localhost:8080";

    /// <summary>
    /// 应用名称
    /// </summary>
    public string AppName { get; set; } = "live";

    /// <summary>
    /// H.264 转码流后缀
    /// </summary>
    public string H264Suffix { get; set; } = "_h264";

    /// <summary>
    /// 预览图存储路径
    /// </summary>
    public string ThumbnailStoragePath { get; set; } = "wwwroot/thumbnails";

    /// <summary>
    /// 预览图截取间隔（秒）
    /// </summary>
    public int ThumbnailIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// FFmpeg 可执行文件路径
    /// </summary>
    public string FFmpegPath { get; set; } = "ffmpeg";
}
