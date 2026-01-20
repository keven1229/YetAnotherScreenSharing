using YASS.Shared.Models;

namespace YASS.Shared.DTOs;

/// <summary>
/// 房间列表响应
/// </summary>
public class RoomListResponse
{
    /// <summary>
    /// 房间列表
    /// </summary>
    public List<RoomInfo> Rooms { get; set; } = [];

    /// <summary>
    /// 总数量
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// 当前页码
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// 每页数量
    /// </summary>
    public int PageSize { get; set; }
}
