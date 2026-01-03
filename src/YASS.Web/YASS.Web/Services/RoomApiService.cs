using System.Net.Http.Json;
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

    public RoomApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    private HttpClient CreateClient() => _httpClientFactory.CreateClient("YassApi");

    public async Task<RoomListResponse?> GetRoomsAsync(int page = 1, int pageSize = 20)
    {
        try
        {
            var client = CreateClient();
            var response = await client.GetFromJsonAsync<ApiResponse<RoomListResponse>>($"/api/rooms?page={page}&pageSize={pageSize}");
            return response?.Data;
        }
        catch
        {
            return null;
        }
    }

    public async Task<RoomInfo?> GetRoomAsync(string roomId)
    {
        try
        {
            var client = CreateClient();
            var response = await client.GetFromJsonAsync<ApiResponse<RoomInfo>>($"/api/rooms/{roomId}");
            return response?.Data;
        }
        catch
        {
            return null;
        }
    }

    public async Task<PlaybackUrls?> GetPlaybackUrlsAsync(string roomId)
    {
        try
        {
            var client = CreateClient();
            var response = await client.GetFromJsonAsync<ApiResponse<PlaybackUrls>>($"/api/rooms/{roomId}/playback");
            return response?.Data;
        }
        catch
        {
            return null;
        }
    }
}
