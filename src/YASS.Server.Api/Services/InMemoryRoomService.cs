using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using YASS.Shared.DTOs;
using YASS.Shared.Enums;
using YASS.Shared.Interfaces;
using YASS.Shared.Models;

namespace YASS.Server.Api.Services;

/// <summary>
/// 房间设置
/// </summary>
public class RoomSettings
{
    /// <summary>
    /// 房间超时时间（分钟），超过此时间未推流的房间会被清理
    /// </summary>
    public int TimeoutMinutes { get; set; } = 30;
}

/// <summary>
/// 内存中的房间服务实现（用于开发/测试）
/// </summary>
public class InMemoryRoomService : IRoomService
{
    private readonly ConcurrentDictionary<string, RoomInfo> _rooms = new();
    private readonly ConcurrentDictionary<string, string> _streamKeys = new(); // roomId -> streamKey
    private readonly SrsSettings _srsSettings;
    private readonly RoomSettings _roomSettings;

    public InMemoryRoomService(IOptions<SrsSettings> srsSettings, IOptions<RoomSettings> roomSettings)
    {
        _srsSettings = srsSettings.Value;
        _roomSettings = roomSettings.Value;
    }

    public Task<CreateRoomResponse> CreateRoomAsync(CreateRoomRequest request, string userId, string userName)
    {
        var roomId = GenerateRoomId();
        var streamKey = GenerateStreamKey();

        var room = new RoomInfo
        {
            Id = roomId,
            Name = request.Name,
            Description = request.Description,
            OwnerId = userId,
            OwnerName = userName,
            Status = RoomStatus.Waiting,
            CreatedAt = DateTime.UtcNow,
            LastActiveAt = DateTime.UtcNow,
            StreamConfig = new StreamConfig()
        };

        _rooms[roomId] = room;
        _streamKeys[roomId] = streamKey;

        var credentials = CreatePublishCredentials(roomId, streamKey);

        return Task.FromResult(new CreateRoomResponse
        {
            Room = room,
            PublishCredentials = credentials
        });
    }

    public Task<RoomInfo?> GetRoomAsync(string roomId)
    {
        _rooms.TryGetValue(roomId, out var room);
        return Task.FromResult(room);
    }

    public Task<RoomListResponse> GetRoomsAsync(int page = 1, int pageSize = 20)
    {
        // 清理超时房间
        CleanupExpiredRooms();

        var allRooms = _rooms.Values.OrderByDescending(r => r.CreatedAt).ToList();
        var totalCount = allRooms.Count;
        var rooms = allRooms.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Task.FromResult(new RoomListResponse
        {
            Rooms = rooms,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// 清理超时未推流的房间
    /// </summary>
    private void CleanupExpiredRooms()
    {
        var timeout = TimeSpan.FromMinutes(_roomSettings.TimeoutMinutes);
        var now = DateTime.UtcNow;

        foreach (var kvp in _rooms)
        {
            var room = kvp.Value;
            // 只清理非直播状态且超时的房间
            if (room.Status != RoomStatus.Live && (now - room.LastActiveAt) > timeout)
            {
                _rooms.TryRemove(kvp.Key, out _);
                _streamKeys.TryRemove(kvp.Key, out _);
            }
        }
    }

    public Task<PlaybackUrls?> GetPlaybackUrlsAsync(string roomId)
    {
        if (!_rooms.TryGetValue(roomId, out var room))
        {
            return Task.FromResult<PlaybackUrls?>(null);
        }

        if (!_streamKeys.TryGetValue(roomId, out var streamKey))
        {
            return Task.FromResult<PlaybackUrls?>(null);
        }

        var streamName = $"{roomId}";
        var app = _srsSettings.AppName;

        return Task.FromResult<PlaybackUrls?>(new PlaybackUrls
        {
            RoomId = roomId,
            RtmpUrl = $"{_srsSettings.RtmpServer}/{app}/{streamName}",
            HttpFlvUrl = $"{_srsSettings.HttpFlvServer}/{app}/{streamName}.flv",
            HttpFlvH264Url = $"{_srsSettings.HttpFlvServer}/{app}/{streamName}{_srsSettings.H264Suffix}.flv",
            HlsUrl = $"{_srsSettings.HlsServer}/{app}/{streamName}.m3u8",
            OriginalCodec = room.StreamConfig?.Codec ?? CodecType.H265
        });
    }

    public Task<bool> UpdateRoomStatusAsync(string roomId, RoomStatus status)
    {
        if (_rooms.TryGetValue(roomId, out var room))
        {
            room.Status = status;
            room.LastActiveAt = DateTime.UtcNow;
            if (status == RoomStatus.Live)
            {
                room.StartedAt = DateTime.UtcNow;
            }
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> DeleteRoomAsync(string roomId)
    {
        var removed = _rooms.TryRemove(roomId, out _);
        _streamKeys.TryRemove(roomId, out _);
        return Task.FromResult(removed);
    }

    public Task<PublishCredentials?> RefreshStreamKeyAsync(string roomId)
    {
        if (!_rooms.ContainsKey(roomId))
        {
            return Task.FromResult<PublishCredentials?>(null);
        }

        var newStreamKey = GenerateStreamKey();
        _streamKeys[roomId] = newStreamKey;

        return Task.FromResult<PublishCredentials?>(CreatePublishCredentials(roomId, newStreamKey));
    }

    private PublishCredentials CreatePublishCredentials(string roomId, string streamKey)
    {
        var app = _srsSettings.AppName;
        var streamName = $"{roomId}?key={streamKey}";

        return new PublishCredentials
        {
            RoomId = roomId,
            StreamKey = streamKey,
            RtmpUrl = $"{_srsSettings.RtmpServer}/{app}",
            FullPublishUrl = $"{_srsSettings.RtmpServer}/{app}/{streamName}",
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };
    }

    private static string GenerateRoomId()
    {
        return Guid.NewGuid().ToString("N")[..8];
    }

    private static string GenerateStreamKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        return Convert.ToHexString(bytes).ToLower();
    }

    public Task<bool> UpdatePrivacyModeAsync(string roomId, bool isPrivacyMode)
    {
        if (_rooms.TryGetValue(roomId, out var room))
        {
            room.IsPrivacyMode = isPrivacyMode;
            if (isPrivacyMode)
            {
                // 启用隐私模式时清除预览图
                room.ThumbnailUrl = null;
                room.ThumbnailUpdatedAt = null;
            }
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> UpdateThumbnailAsync(string roomId, string? thumbnailUrl)
    {
        if (_rooms.TryGetValue(roomId, out var room))
        {
            room.ThumbnailUrl = thumbnailUrl;
            room.ThumbnailUpdatedAt = thumbnailUrl != null ? DateTime.UtcNow : null;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
}
