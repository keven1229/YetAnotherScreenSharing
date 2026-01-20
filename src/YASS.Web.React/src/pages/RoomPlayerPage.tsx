import { useParams, Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { getRoom, getPlaybackUrls } from '../services/api';
import { RoomStatus, RoomStatusInfo } from '../types';
import VideoPlayer from '../components/VideoPlayer';

export default function RoomPlayerPage() {
  const { roomId } = useParams<{ roomId: string }>();

  // 获取房间信息
  const {
    data: room,
    isLoading: isLoadingRoom,
    isError: isRoomError,
    error: roomError,
    refetch: refetchRoom,
  } = useQuery({
    queryKey: ['room', roomId],
    queryFn: () => getRoom(roomId!),
    enabled: !!roomId,
    refetchInterval: (query) => {
      // 如果房间状态是等待中，每5秒刷新一次
      const room = query.state.data;
      return room?.status === RoomStatus.Waiting ? 5000 : false;
    },
  });

  // 获取播放地址（仅当房间状态为直播中时）
  const {
    data: playbackUrls,
    isLoading: isLoadingPlayback,
    isError: isPlaybackError,
  } = useQuery({
    queryKey: ['playback', roomId],
    queryFn: () => getPlaybackUrls(roomId!),
    enabled: !!roomId && room?.status === RoomStatus.Live,
  });

  const isLive = room?.status === RoomStatus.Live;
  const statusInfo = room ? RoomStatusInfo[room.status] : null;

  // 获取播放URL（优先使用原始HTTP-FLV流，因为源流通常已是H.264编码）
  const getPlayUrl = (): string | null => {
    if (!playbackUrls) return null;
    // 优先使用原始流，如果需要转码流则使用 httpFlvH264Url
    return playbackUrls.httpFlvUrl || playbackUrls.httpFlvH264Url || null;
  };

  const playUrl = getPlayUrl();

  return (
    <div className="min-h-screen bg-gray-900">
      {/* 头部导航 */}
      <header className="bg-gray-800 border-b border-gray-700">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-4">
          <div className="flex items-center gap-4">
            <Link
              to="/"
              className="flex items-center gap-2 text-gray-300 hover:text-white transition-colors"
            >
              <svg
                className="w-5 h-5"
                fill="none"
                stroke="currentColor"
                viewBox="0 0 24 24"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M10 19l-7-7m0 0l7-7m-7 7h18"
                />
              </svg>
              返回列表
            </Link>
            <div className="h-5 w-px bg-gray-600" />
            <span className="text-white font-medium truncate">
              {room?.name || '加载中...'}
            </span>
            {statusInfo && (
              <span
                className={`inline-flex items-center px-2 py-1 rounded-full text-xs font-medium text-white ${statusInfo.color}`}
              >
                {isLive && (
                  <span className="w-2 h-2 mr-1 bg-white rounded-full animate-pulse" />
                )}
                {statusInfo.label}
              </span>
            )}
          </div>
        </div>
      </header>

      {/* 主内容区域 */}
      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-6">
        <div className="grid grid-cols-1 lg:grid-cols-4 gap-6">
          {/* 播放器区域 */}
          <div className="lg:col-span-3">
            <div className="bg-black rounded-xl overflow-hidden aspect-video">
              {/* 加载状态 */}
              {(isLoadingRoom || isLoadingPlayback) && (
                <div className="w-full h-full flex items-center justify-center">
                  <div className="flex flex-col items-center gap-4">
                    <div className="w-12 h-12 border-4 border-blue-500 border-t-transparent rounded-full animate-spin" />
                    <p className="text-gray-300">加载中...</p>
                  </div>
                </div>
              )}

              {/* 错误状态 */}
              {isRoomError && (
                <div className="w-full h-full flex items-center justify-center">
                  <div className="text-center">
                    <svg
                      className="w-16 h-16 mx-auto text-red-500"
                      fill="none"
                      stroke="currentColor"
                      viewBox="0 0 24 24"
                    >
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth={2}
                        d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
                      />
                    </svg>
                    <h2 className="mt-4 text-lg font-semibold text-white">
                      加载失败
                    </h2>
                    <p className="mt-2 text-gray-400">
                      {roomError instanceof Error ? roomError.message : '房间不存在或已删除'}
                    </p>
                    <button
                      onClick={() => refetchRoom()}
                      className="mt-4 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors"
                    >
                      重试
                    </button>
                  </div>
                </div>
              )}

              {/* 等待直播状态 */}
              {room && room.status === RoomStatus.Waiting && (
                <div className="w-full h-full flex items-center justify-center">
                  <div className="text-center">
                    <svg
                      className="w-20 h-20 mx-auto text-yellow-500"
                      fill="none"
                      stroke="currentColor"
                      viewBox="0 0 24 24"
                    >
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth={1.5}
                        d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"
                      />
                    </svg>
                    <h2 className="mt-4 text-xl font-semibold text-white">
                      等待直播开始
                    </h2>
                    <p className="mt-2 text-gray-400">
                      主播尚未开始推流，请稍候...
                    </p>
                    <div className="mt-4 flex items-center justify-center gap-2 text-gray-500">
                      <div className="w-2 h-2 bg-yellow-500 rounded-full animate-bounce" style={{ animationDelay: '0ms' }} />
                      <div className="w-2 h-2 bg-yellow-500 rounded-full animate-bounce" style={{ animationDelay: '150ms' }} />
                      <div className="w-2 h-2 bg-yellow-500 rounded-full animate-bounce" style={{ animationDelay: '300ms' }} />
                    </div>
                  </div>
                </div>
              )}

              {/* 直播已结束状态 */}
              {room && room.status === RoomStatus.Ended && (
                <div className="w-full h-full flex items-center justify-center">
                  <div className="text-center">
                    <svg
                      className="w-20 h-20 mx-auto text-gray-500"
                      fill="none"
                      stroke="currentColor"
                      viewBox="0 0 24 24"
                    >
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth={1.5}
                        d="M21 12a9 9 0 11-18 0 9 9 0 0118 0z"
                      />
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth={1.5}
                        d="M9 10a1 1 0 011-1h4a1 1 0 011 1v4a1 1 0 01-1 1h-4a1 1 0 01-1-1v-4z"
                      />
                    </svg>
                    <h2 className="mt-4 text-xl font-semibold text-white">
                      直播已结束
                    </h2>
                    <p className="mt-2 text-gray-400">
                      感谢您的观看
                    </p>
                    <Link
                      to="/"
                      className="mt-4 inline-block px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors"
                    >
                      返回房间列表
                    </Link>
                  </div>
                </div>
              )}

              {/* 视频播放器 */}
              {isLive && playUrl && !isPlaybackError && (
                <VideoPlayer
                  url={playUrl}
                  autoPlay
                  muted
                  width="100%"
                  height="100%"
                  onError={(err) => console.error('播放错误:', err)}
                />
              )}

              {/* 播放地址获取失败 */}
              {isLive && isPlaybackError && (
                <div className="w-full h-full flex items-center justify-center">
                  <div className="text-center">
                    <svg
                      className="w-16 h-16 mx-auto text-orange-500"
                      fill="none"
                      stroke="currentColor"
                      viewBox="0 0 24 24"
                    >
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth={2}
                        d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
                      />
                    </svg>
                    <h2 className="mt-4 text-lg font-semibold text-white">
                      无法获取播放地址
                    </h2>
                    <p className="mt-2 text-gray-400">
                      请稍后重试
                    </p>
                  </div>
                </div>
              )}
            </div>
          </div>

          {/* 信息面板 */}
          <div className="lg:col-span-1">
            <div className="bg-gray-800 rounded-xl p-4">
              <h2 className="text-lg font-semibold text-white mb-4">房间信息</h2>
              
              {room ? (
                <div className="space-y-4">
                  <div>
                    <label className="text-xs text-gray-400 uppercase tracking-wide">
                      房间名称
                    </label>
                    <p className="mt-1 text-white">{room.name}</p>
                  </div>

                  {room.description && (
                    <div>
                      <label className="text-xs text-gray-400 uppercase tracking-wide">
                        描述
                      </label>
                      <p className="mt-1 text-gray-300 text-sm">{room.description}</p>
                    </div>
                  )}

                  <div>
                    <label className="text-xs text-gray-400 uppercase tracking-wide">
                      主播
                    </label>
                    <p className="mt-1 text-white flex items-center gap-2">
                      <svg className="w-4 h-4" fill="currentColor" viewBox="0 0 24 24">
                        <path d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z" />
                      </svg>
                      {room.ownerName}
                    </p>
                  </div>

                  {isLive && (
                    <div>
                      <label className="text-xs text-gray-400 uppercase tracking-wide">
                        观看人数
                      </label>
                      <p className="mt-1 text-white flex items-center gap-2">
                        <svg className="w-4 h-4" fill="currentColor" viewBox="0 0 24 24">
                          <path d="M12 4.5C7 4.5 2.73 7.61 1 12c1.73 4.39 6 7.5 11 7.5s9.27-3.11 11-7.5c-1.73-4.39-6-7.5-11-7.5zM12 17c-2.76 0-5-2.24-5-5s2.24-5 5-5 5 2.24 5 5-2.24 5-5 5zm0-8c-1.66 0-3 1.34-3 3s1.34 3 3 3 3-1.34 3-3-1.34-3-3-3z" />
                        </svg>
                        {room.viewerCount} 人
                      </p>
                    </div>
                  )}

                  {room.streamConfig && (
                    <div>
                      <label className="text-xs text-gray-400 uppercase tracking-wide">
                        画质
                      </label>
                      <p className="mt-1 text-gray-300 text-sm">
                        {room.streamConfig.width}x{room.streamConfig.height} @ {room.streamConfig.frameRate}fps
                      </p>
                    </div>
                  )}

                  <div>
                    <label className="text-xs text-gray-400 uppercase tracking-wide">
                      创建时间
                    </label>
                    <p className="mt-1 text-gray-300 text-sm">
                      {new Date(room.createdAt).toLocaleString('zh-CN')}
                    </p>
                  </div>

                  {room.startedAt && (
                    <div>
                      <label className="text-xs text-gray-400 uppercase tracking-wide">
                        开播时间
                      </label>
                      <p className="mt-1 text-gray-300 text-sm">
                        {new Date(room.startedAt).toLocaleString('zh-CN')}
                      </p>
                    </div>
                  )}
                </div>
              ) : (
                <div className="animate-pulse space-y-4">
                  <div className="h-4 bg-gray-700 rounded w-3/4" />
                  <div className="h-4 bg-gray-700 rounded w-1/2" />
                  <div className="h-4 bg-gray-700 rounded w-2/3" />
                </div>
              )}
            </div>

            {/* 播放地址信息 */}
            {playbackUrls && (
              <div className="mt-4 bg-gray-800 rounded-xl p-4">
                <h3 className="text-sm font-medium text-gray-400 mb-3">播放地址</h3>
                <div className="space-y-2">
                  {playbackUrls.httpFlvH264Url && (
                    <div className="text-xs">
                      <span className="text-gray-500">HTTP-FLV (H.264):</span>
                      <p className="mt-1 text-gray-300 break-all font-mono text-xs bg-gray-900 p-2 rounded">
                        {playbackUrls.httpFlvH264Url}
                      </p>
                    </div>
                  )}
                  {playbackUrls.hlsUrl && (
                    <div className="text-xs">
                      <span className="text-gray-500">HLS:</span>
                      <p className="mt-1 text-gray-300 break-all font-mono text-xs bg-gray-900 p-2 rounded">
                        {playbackUrls.hlsUrl}
                      </p>
                    </div>
                  )}
                </div>
              </div>
            )}
          </div>
        </div>
      </main>
    </div>
  );
}
