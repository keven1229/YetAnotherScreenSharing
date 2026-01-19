using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using YASS.Shared.DTOs;
using YASS.Shared.Models;

namespace YASS.Web.Services;

/// <summary>
/// 房间 API 服务接口
/// </summary>
public interface IRoomApiService
{
    Task<RoomListResponse?> GetRoomsAsync(int page = 1, int pageSize = 20);
    Task<RoomInfo?> GetRoomAsync(string roomId);
    Task<PlaybackUrls?> GetPlaybackUrlsAsync(string roomId);
}

/// <summary>
/// 房间 API 服务实现
/// </summary>
public class RoomApiService : IRoomApiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RoomApiService> _logger;

    public RoomApiService(IHttpClientFactory httpClientFactory, ILogger<RoomApiService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private HttpClient CreateClient() => _httpClientFactory.CreateClient("YassApi");

    public async Task<RoomListResponse?> GetRoomsAsync(int page = 1, int pageSize = 20)
    {
        try
        {
            var client = CreateClient();
            _logger.LogInformation("Fetching rooms from {BaseAddress}", client.BaseAddress);
            var response = await client.GetFromJsonAsync<ApiResponse<RoomListResponse>>($"/api/rooms?page={page}&pageSize={pageSize}");
            _logger.LogInformation("Rooms response: Success={Success}, Count={Count}", response?.Success, response?.Data?.Rooms?.Count);
            return response?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch rooms");
            return null;
        }
    }

    public async Task<RoomInfo?> GetRoomAsync(string roomId)
    {
        try
        {
            var client = CreateClient();
            _logger.LogInformation("Fetching room {RoomId} from {BaseAddress}", roomId, client.BaseAddress);
            var response = await client.GetFromJsonAsync<ApiResponse<RoomInfo>>($"/api/rooms/{roomId}");
            _logger.LogInformation("Room response: Success={Success}, Name={Name}", response?.Success, response?.Data?.Name);
            return response?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch room {RoomId}", roomId);
            return null;
        }
    }

    public async Task<PlaybackUrls?> GetPlaybackUrlsAsync(string roomId)
    {
        try
        {
            var client = CreateClient();
            _logger.LogInformation("Fetching playback URLs for room {RoomId}", roomId);
            var response = await client.GetFromJsonAsync<ApiResponse<PlaybackUrls>>($"/api/rooms/{roomId}/playback");
            _logger.LogInformation("Playback response: Success={Success}, FlvUrl={FlvUrl}", response?.Success, response?.Data?.HttpFlvUrl);
            return response?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch playback URLs for room {RoomId}", roomId);
            return null;
        }
    }
}
