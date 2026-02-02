using Microsoft.AspNetCore.Mvc;
using YASS.Server.Api.Services;
using YASS.Shared.DTOs;
using YASS.Shared.Interfaces;

namespace YASS.Server.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoomsController : ControllerBase
{
    private readonly IRoomService _roomService;
    private readonly IAuthService _authService;
    private readonly ThumbnailService _thumbnailService;
    private readonly ILogger<RoomsController> _logger;

    public RoomsController(
        IRoomService roomService,
        IAuthService authService,
        ThumbnailService thumbnailService,
        ILogger<RoomsController> logger)
    {
        _roomService = roomService;
        _authService = authService;
        _thumbnailService = thumbnailService;
        _logger = logger;
    }

    /// <summary>
    /// 获取房间列表
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<RoomListResponse>>> GetRooms(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var response = await _roomService.GetRoomsAsync(page, pageSize);
        return Ok(ApiResponse<RoomListResponse>.Ok(response));
    }

    /// <summary>
    /// 获取房间详情
    /// </summary>
    [HttpGet("{roomId}")]
    public async Task<ActionResult<ApiResponse<Shared.Models.RoomInfo>>> GetRoom(string roomId)
    {
        var room = await _roomService.GetRoomAsync(roomId);
        if (room == null)
        {
            return NotFound(ApiResponse<Shared.Models.RoomInfo>.Fail("房间不存在", "ROOM_NOT_FOUND"));
        }
        return Ok(ApiResponse<Shared.Models.RoomInfo>.Ok(room));
    }

    /// <summary>
    /// 创建房间
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<CreateRoomResponse>>> CreateRoom([FromBody] CreateRoomRequest request)
    {
        // 获取当前用户信息（开发模式使用默认用户）
        var userInfo = await _authService.GetUserInfoAsync(User);
        var userId = userInfo?.Id ?? "anonymous";
        var userName = userInfo?.UserName ?? "Anonymous";

        var response = await _roomService.CreateRoomAsync(request, userId, userName);
        _logger.LogInformation("Room created: {RoomId} by {UserId}", response.Room.Id, userId);

        return CreatedAtAction(nameof(GetRoom), new { roomId = response.Room.Id },
            ApiResponse<CreateRoomResponse>.Ok(response));
    }

    /// <summary>
    /// 删除房间
    /// </summary>
    [HttpDelete("{roomId}")]
    public async Task<ActionResult<ApiResponse>> DeleteRoom(string roomId)
    {
        var room = await _roomService.GetRoomAsync(roomId);
        if (room == null)
        {
            return NotFound(ApiResponse.Fail("房间不存在", "ROOM_NOT_FOUND"));
        }

        // 检查权限
        var userInfo = await _authService.GetUserInfoAsync(User);
        var hasPermission = await _authService.HasPermissionAsync(
            userInfo?.Id ?? "", roomId, RoomPermission.Manage);

        if (!hasPermission)
        {
            return Forbid();
        }

        await _roomService.DeleteRoomAsync(roomId);
        return Ok(ApiResponse.Ok());
    }

    /// <summary>
    /// 获取房间播放地址
    /// </summary>
    [HttpGet("{roomId}/playback")]
    public async Task<ActionResult<ApiResponse<Shared.Models.PlaybackUrls>>> GetPlaybackUrls(string roomId)
    {
        var urls = await _roomService.GetPlaybackUrlsAsync(roomId);
        if (urls == null)
        {
            return NotFound(ApiResponse<Shared.Models.PlaybackUrls>.Fail("房间不存在", "ROOM_NOT_FOUND"));
        }
        return Ok(ApiResponse<Shared.Models.PlaybackUrls>.Ok(urls));
    }

    /// <summary>
    /// 获取推流凭证
    /// </summary>
    [HttpGet("{roomId}/publish")]
    public async Task<ActionResult<ApiResponse<Shared.Models.PublishCredentials>>> GetPublishCredentials(string roomId)
    {
        var room = await _roomService.GetRoomAsync(roomId);
        if (room == null)
        {
            return NotFound(ApiResponse<Shared.Models.PublishCredentials>.Fail("房间不存在", "ROOM_NOT_FOUND"));
        }

        // 检查权限
        var userInfo = await _authService.GetUserInfoAsync(User);
        var hasPermission = await _authService.HasPermissionAsync(
            userInfo?.Id ?? "", roomId, RoomPermission.Publish);

        if (!hasPermission)
        {
            return Forbid();
        }

        var credentials = await _roomService.RefreshStreamKeyAsync(roomId);
        if (credentials == null)
        {
            return NotFound(ApiResponse<Shared.Models.PublishCredentials>.Fail("获取凭证失败", "CREDENTIALS_ERROR"));
        }

        return Ok(ApiResponse<Shared.Models.PublishCredentials>.Ok(credentials));
    }

    /// <summary>
    /// 刷新推流密钥
    /// </summary>
    [HttpPost("{roomId}/refresh-key")]
    public async Task<ActionResult<ApiResponse<Shared.Models.PublishCredentials>>> RefreshStreamKey(string roomId)
    {
        var room = await _roomService.GetRoomAsync(roomId);
        if (room == null)
        {
            return NotFound(ApiResponse<Shared.Models.PublishCredentials>.Fail("房间不存在", "ROOM_NOT_FOUND"));
        }

        // 检查权限
        var userInfo = await _authService.GetUserInfoAsync(User);
        var hasPermission = await _authService.HasPermissionAsync(
            userInfo?.Id ?? "", roomId, RoomPermission.Manage);

        if (!hasPermission)
        {
            return Forbid();
        }

        var credentials = await _roomService.RefreshStreamKeyAsync(roomId);
        return Ok(ApiResponse<Shared.Models.PublishCredentials>.Ok(credentials!));
    }

    /// <summary>
    /// 切换房间隐私模式
    /// </summary>
    [HttpPatch("{roomId}/privacy")]
    public async Task<ActionResult<ApiResponse>> UpdatePrivacyMode(
        string roomId, 
        [FromBody] UpdatePrivacyModeRequest request)
    {
        var room = await _roomService.GetRoomAsync(roomId);
        if (room == null)
        {
            return NotFound(ApiResponse.Fail("房间不存在", "ROOM_NOT_FOUND"));
        }

        // 检查权限
        var userInfo = await _authService.GetUserInfoAsync(User);
        var hasPermission = await _authService.HasPermissionAsync(
            userInfo?.Id ?? "", roomId, RoomPermission.Manage);

        if (!hasPermission)
        {
            return Forbid();
        }

        var updated = await _roomService.UpdatePrivacyModeAsync(roomId, request.IsPrivacyMode);
        if (!updated)
        {
            return BadRequest(ApiResponse.Fail("更新失败", "UPDATE_FAILED"));
        }

        // 如果启用隐私模式，停止截图；否则如果房间正在直播，启动截图
        if (request.IsPrivacyMode)
        {
            _thumbnailService.StopCapture(roomId);
        }
        else if (room.Status == Shared.Enums.RoomStatus.Live)
        {
            _thumbnailService.StartCapture(roomId);
        }

        _logger.LogInformation("Room {RoomId} privacy mode updated to {IsPrivacyMode}", roomId, request.IsPrivacyMode);
        return Ok(ApiResponse.Ok());
    }
}

/// <summary>
/// 更新隐私模式请求
/// </summary>
public class UpdatePrivacyModeRequest
{
    public bool IsPrivacyMode { get; set; }
}
