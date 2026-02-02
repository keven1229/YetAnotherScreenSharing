namespace YASS.Shared.DTOs;

/// <summary>
/// 创建房间请求
/// </summary>
public class CreateRoomRequest
{
    /// <summary>
    /// 房间名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 房间描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 是否启用隐私模式（启用后不在房间列表显示预览图）
    /// </summary>
    public bool IsPrivacyMode { get; set; }
}
