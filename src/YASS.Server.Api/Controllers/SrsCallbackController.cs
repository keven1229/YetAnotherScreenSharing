using Microsoft.AspNetCore.Mvc;
using YASS.Shared.Enums;
using YASS.Shared.Interfaces;

namespace YASS.Server.Api.Controllers;

/// <summary>
/// SRS 回调控制器 - 处理推流事件
/// </summary>
[ApiController]
[Route("api/srs")]
public class SrsCallbackController : ControllerBase
{
    private readonly IRoomService _roomService;
    private readonly ILogger<SrsCallbackController> _logger;

    public SrsCallbackController(
        IRoomService roomService,
        ILogger<SrsCallbackController> logger)
    {
        _roomService = roomService;
        _logger = logger;
    }

    /// <summary>
    /// SRS 推流开始回调
    /// </summary>
    [HttpPost("on_publish")]
    public async Task<IActionResult> OnPublish([FromBody] SrsCallbackRequest request)
    {
        _logger.LogInformation("SRS on_publish: app={App}, stream={Stream}, client_id={ClientId}", 
            request.App, request.Stream, request.ClientId);

        // 从流名称中提取房间ID (格式: roomId?key=xxx)
        var roomId = ExtractRoomId(request.Stream);
        if (string.IsNullOrEmpty(roomId))
        {
            _logger.LogWarning("Cannot extract room ID from stream: {Stream}", request.Stream);
            return Ok(new { code = 0 });
        }

        // 更新房间状态为直播中
        var updated = await _roomService.UpdateRoomStatusAsync(roomId, RoomStatus.Live);
        if (updated)
        {
            _logger.LogInformation("Room {RoomId} status updated to Live", roomId);
        }
        else
        {
            _logger.LogWarning("Failed to update room {RoomId} status", roomId);
        }

        return Ok(new { code = 0 });
    }

    /// <summary>
    /// SRS 推流结束回调
    /// </summary>
    [HttpPost("on_unpublish")]
    public async Task<IActionResult> OnUnpublish([FromBody] SrsCallbackRequest request)
    {
        _logger.LogInformation("SRS on_unpublish: app={App}, stream={Stream}", 
            request.App, request.Stream);

        var roomId = ExtractRoomId(request.Stream);
        if (string.IsNullOrEmpty(roomId))
        {
            return Ok(new { code = 0 });
        }

        // 更新房间状态为等待中
        var updated = await _roomService.UpdateRoomStatusAsync(roomId, RoomStatus.Waiting);
        if (updated)
        {
            _logger.LogInformation("Room {RoomId} status updated to Waiting", roomId);
        }

        return Ok(new { code = 0 });
    }

    /// <summary>
    /// SRS 客户端连接回调
    /// </summary>
    [HttpPost("on_connect")]
    public IActionResult OnConnect([FromBody] SrsCallbackRequest request)
    {
        _logger.LogDebug("SRS on_connect: client_id={ClientId}, ip={Ip}", 
            request.ClientId, request.Ip);
        return Ok(new { code = 0 });
    }

    /// <summary>
    /// SRS 播放开始回调
    /// </summary>
    [HttpPost("on_play")]
    public IActionResult OnPlay([FromBody] SrsCallbackRequest request)
    {
        _logger.LogDebug("SRS on_play: app={App}, stream={Stream}", 
            request.App, request.Stream);
        return Ok(new { code = 0 });
    }

    /// <summary>
    /// SRS 播放结束回调
    /// </summary>
    [HttpPost("on_stop")]
    public IActionResult OnStop([FromBody] SrsCallbackRequest request)
    {
        _logger.LogDebug("SRS on_stop: app={App}, stream={Stream}", 
            request.App, request.Stream);
        return Ok(new { code = 0 });
    }

    /// <summary>
    /// 从流名称中提取房间ID
    /// </summary>
    private static string? ExtractRoomId(string? stream)
    {
        if (string.IsNullOrEmpty(stream))
            return null;

        // 流名称格式: roomId?key=xxx 或 roomId
        var questionIndex = stream.IndexOf('?');
        return questionIndex > 0 ? stream.Substring(0, questionIndex) : stream;
    }
}

/// <summary>
/// SRS 回调请求模型
/// </summary>
public class SrsCallbackRequest
{
    public string? Action { get; set; }
    public string? ClientId { get; set; }
    public string? Ip { get; set; }
    public string? Vhost { get; set; }
    public string? App { get; set; }
    public string? Stream { get; set; }
    public string? Param { get; set; }
    public string? Tcurl { get; set; }
    public string? PageUrl { get; set; }
}
