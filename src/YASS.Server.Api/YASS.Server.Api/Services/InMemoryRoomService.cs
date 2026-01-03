using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using YASS.Shared.DTOs;
using YASS.Shared.Enums;
using YASS.Shared.Interfaces;
using YASS.Shared.Models;

namespace YASS.Server.Api.Services;

/// <summary>
/// 内存中的房间服务实现（用于开发/测试）
/// </summary>
public class InMemoryRoomService : IRoomService
{
    private readonly ConcurrentDictionary<string, RoomInfo> _rooms = new();
    private readonly ConcurrentDictionary<string, string> _streamKeys = new(); // roomId -> streamKey
    private readonly SrsSettings _srsSettings;

    public InMemoryRoomService(IOptions<SrsSettings> srsSettings)
    {
        _srsSettings = srsSettings.Value;
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
}
