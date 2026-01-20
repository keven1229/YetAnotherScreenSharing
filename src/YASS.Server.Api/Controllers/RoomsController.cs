using Microsoft.AspNetCore.Mvc;
using YASS.Shared.DTOs;
using YASS.Shared.Interfaces;

namespace YASS.Server.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoomsController : ControllerBase
{
    private readonly IRoomService _roomService;
    private readonly IAuthService _authService;
    private readonly ILogger<RoomsController> _logger;

    public RoomsController(
        IRoomService roomService,
        IAuthService authService,
        ILogger<RoomsController> logger)
    {
        _roomService = roomService;
        _authService = authService;
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
}
