using YASS.Shared.Enums;

namespace YASS.Shared.Models;

/// <summary>
/// 房间信息
/// </summary>
public class RoomInfo
{
    /// <summary>
    /// 房间唯一标识
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 房间名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 房间描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 创建者用户ID
    /// </summary>
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>
    /// 创建者用户名
    /// </summary>
    public string OwnerName { get; set; } = string.Empty;

    /// <summary>
    /// 房间状态
    /// </summary>
    public RoomStatus Status { get; set; } = RoomStatus.Waiting;

    /// <summary>
    /// 当前观看人数
    /// </summary>
    public int ViewerCount { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 开始直播时间
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// 最后活跃时间（用于超时清理）
    /// </summary>
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 流配置信息
    /// </summary>
    public StreamConfig? StreamConfig { get; set; }

    /// <summary>
    /// 预览图URL
    /// </summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// 预览图更新时间
    /// </summary>
    public DateTime? ThumbnailUpdatedAt { get; set; }

    /// <summary>
    /// 是否启用隐私模式（启用后不在房间列表显示预览图）
    /// </summary>
    public bool IsPrivacyMode { get; set; }
}
