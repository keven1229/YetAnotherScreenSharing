namespace YASS.Shared.Enums;

/// <summary>
/// 房间状态
/// </summary>
public enum RoomStatus
{
    /// <summary>
    /// 等待推流
    /// </summary>
    Waiting = 0,

    /// <summary>
    /// 正在直播
    /// </summary>
    Live = 1,

    /// <summary>
    /// 已结束
    /// </summary>
    Ended = 2
}
