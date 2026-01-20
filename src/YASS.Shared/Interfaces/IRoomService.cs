using YASS.Shared.DTOs;
using YASS.Shared.Models;

namespace YASS.Shared.Interfaces;

/// <summary>
/// 房间服务接口
/// </summary>
public interface IRoomService
{
    /// <summary>
    /// 创建房间
    /// </summary>
    Task<CreateRoomResponse> CreateRoomAsync(CreateRoomRequest request, string userId, string userName);

    /// <summary>
    /// 获取房间信息
    /// </summary>
    Task<RoomInfo?> GetRoomAsync(string roomId);

    /// <summary>
    /// 获取房间列表
    /// </summary>
    Task<RoomListResponse> GetRoomsAsync(int page = 1, int pageSize = 20);

    /// <summary>
    /// 获取房间播放地址
    /// </summary>
    Task<PlaybackUrls?> GetPlaybackUrlsAsync(string roomId);

    /// <summary>
    /// 更新房间状态
    /// </summary>
    Task<bool> UpdateRoomStatusAsync(string roomId, Enums.RoomStatus status);

    /// <summary>
    /// 删除房间
    /// </summary>
    Task<bool> DeleteRoomAsync(string roomId);

    /// <summary>
    /// 刷新推流密钥
    /// </summary>
    Task<PublishCredentials?> RefreshStreamKeyAsync(string roomId);
}
