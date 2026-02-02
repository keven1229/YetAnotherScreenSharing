using System.Net.Http;
using System.Net.Http.Json;
using YASS.Shared.DTOs;
using YASS.Shared.Models;

namespace YASS.Client.Desktop.Services;

/// <summary>
/// API 服务接口
/// </summary>
public interface IApiService
{
    Task<RoomListResponse?> GetRoomsAsync(int page = 1, int pageSize = 20);
    Task<CreateRoomResponse?> CreateRoomAsync(CreateRoomRequest request);
    Task<RoomInfo?> GetRoomAsync(string roomId);
    Task<PlaybackUrls?> GetPlaybackUrlsAsync(string roomId);
    Task<PublishCredentials?> GetPublishCredentialsAsync(string roomId);
    Task<bool> DeleteRoomAsync(string roomId);
}

/// <summary>
/// API 服务实现
/// </summary>
public class ApiService : IApiService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ApiService(IHttpClientFactory httpClientFactory)
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
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException($"无法连接到API服务器 (http://localhost:5000): {ex.Message}", ex);
        }
    }

    public async Task<CreateRoomResponse?> CreateRoomAsync(CreateRoomRequest request)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/rooms", request);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"API returned {response.StatusCode}: {errorContent}");
        }
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<CreateRoomResponse>>();
        return result?.Data;
    }

    public async Task<RoomInfo?> GetRoomAsync(string roomId)
    {
        var client = CreateClient();
        var response = await client.GetFromJsonAsync<ApiResponse<RoomInfo>>($"/api/rooms/{roomId}");
        return response?.Data;
    }

    public async Task<PlaybackUrls?> GetPlaybackUrlsAsync(string roomId)
    {
        var client = CreateClient();
        var response = await client.GetFromJsonAsync<ApiResponse<PlaybackUrls>>($"/api/rooms/{roomId}/playback");
        return response?.Data;
    }

    public async Task<PublishCredentials?> GetPublishCredentialsAsync(string roomId)
    {
        var client = CreateClient();
        var response = await client.GetFromJsonAsync<ApiResponse<PublishCredentials>>($"/api/rooms/{roomId}/publish");
        return response?.Data;
    }

    public async Task<bool> DeleteRoomAsync(string roomId)
    {
        var client = CreateClient();
        var response = await client.DeleteAsync($"/api/rooms/{roomId}");
        
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        return result?.Success ?? false;
    }
}
