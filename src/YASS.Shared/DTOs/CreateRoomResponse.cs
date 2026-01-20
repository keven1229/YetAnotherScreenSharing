using YASS.Shared.Models;

namespace YASS.Shared.DTOs;

/// <summary>
/// 创建房间响应
/// </summary>
public class CreateRoomResponse
{
    /// <summary>
    /// 房间信息
    /// </summary>
    public RoomInfo Room { get; set; } = new();

    /// <summary>
    /// 推流凭证
    /// </summary>
    public PublishCredentials PublishCredentials { get; set; } = new();
}
