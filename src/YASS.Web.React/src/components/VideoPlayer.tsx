import { useEffect, useRef, useState, useCallback } from 'react';
import mpegts from 'mpegts.js';

interface VideoPlayerProps {
  /** HTTP-FLV 播放地址 */
  url: string;
  /** 是否自动播放 */
  autoPlay?: boolean;
  /** 是否静音 */
  muted?: boolean;
  /** 播放器宽度 */
  width?: string | number;
  /** 播放器高度 */
  height?: string | number;
  /** 错误回调 */
  onError?: (error: Error) => void;
  /** 播放开始回调 */
  onPlay?: () => void;
  /** 播放暂停回调 */
  onPause?: () => void;
  /** 自动重连次数限制 */
  maxReconnectAttempts?: number;
  /** 重连间隔（毫秒） */
  reconnectInterval?: number;
}

type PlayerError = {
  type: string;
  details: string;
  fatal: boolean;
};

export default function VideoPlayer({
  url,
  autoPlay = true,
  muted = true,
  width = '100%',
  height = 'auto',
  onError,
  onPlay,
  onPause,
  maxReconnectAttempts = 5,
  reconnectInterval = 2000,
}: VideoPlayerProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const videoRef = useRef<HTMLVideoElement>(null);
  const playerRef = useRef<mpegts.Player | null>(null);
  const reconnectTimerRef = useRef<number | null>(null);
  const reconnectAttemptsRef = useRef(0);
  const [isPlaying, setIsPlaying] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isReconnecting, setIsReconnecting] = useState(false);
  const [isMuted, setIsMuted] = useState(muted);
  const [volume, setVolume] = useState(1);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [showControls, setShowControls] = useState(true);
  const hideControlsTimerRef = useRef<number | null>(null);

  // 销毁播放器
  const destroyPlayer = useCallback(() => {
    if (reconnectTimerRef.current) {
      clearTimeout(reconnectTimerRef.current);
      reconnectTimerRef.current = null;
    }
    if (playerRef.current) {
      playerRef.current.pause();
      playerRef.current.unload();
      playerRef.current.detachMediaElement();
      playerRef.current.destroy();
      playerRef.current = null;
    }
  }, []);

  // 初始化播放器
  const initPlayer = useCallback((isReconnect = false) => {
    if (!videoRef.current || !url) return;

    // 检查浏览器支持
    if (!mpegts.isSupported()) {
      const err = new Error('您的浏览器不支持 FLV 播放');
      setError(err.message);
      onError?.(err);
      return;
    }

    // 销毁旧播放器
    destroyPlayer();

    setIsLoading(true);
    setError(null);
    if (isReconnect) {
      setIsReconnecting(true);
    }

    try {
      // 创建播放器
      const player = mpegts.createPlayer(
        {
          type: 'flv',
          url: url,
          isLive: true,
          hasAudio: true,
          hasVideo: true,
          cors: true,
        },
        {
          enableWorker: true,
          enableStashBuffer: false,
          stashInitialSize: 128,
          lazyLoad: false,
          lazyLoadMaxDuration: 0,
          liveBufferLatencyChasing: true,
          liveBufferLatencyMaxLatency: 1.5,
          liveBufferLatencyMinRemain: 0.3,
        }
      );

      playerRef.current = player;

      // 绑定到 video 元素
      player.attachMediaElement(videoRef.current);

      // 事件监听
      player.on(mpegts.Events.ERROR, (errorType: string, errorDetail: string, errorInfo: PlayerError) => {
        console.error('mpegts.js error:', errorType, errorDetail, errorInfo);
        // 尝试自动重连
        if (reconnectAttemptsRef.current < maxReconnectAttempts) {
          console.log(`播放错误，${reconnectInterval}ms 后尝试重连 (${reconnectAttemptsRef.current + 1}/${maxReconnectAttempts})`);
          reconnectTimerRef.current = window.setTimeout(() => {
            reconnectAttemptsRef.current++;
            initPlayer(true);
          }, reconnectInterval);
        } else {
          const err = new Error(`播放错误: ${errorDetail}`);
          setError(err.message);
          setIsLoading(false);
          setIsReconnecting(false);
          onError?.(err);
        }
      });

      player.on(mpegts.Events.LOADING_COMPLETE, () => {
        console.log('Loading complete');
      });

      player.on(mpegts.Events.METADATA_ARRIVED, () => {
        setIsLoading(false);
        setIsReconnecting(false);
        reconnectAttemptsRef.current = 0; // 重置重连计数
      });

      // 加载并播放
      player.load();
      
      if (autoPlay) {
        player.play();
      }
    } catch (err) {
      const error = err instanceof Error ? err : new Error('播放器初始化失败');
      setError(error.message);
      setIsLoading(false);
      setIsReconnecting(false);
      onError?.(error);
    }
  }, [url, autoPlay, destroyPlayer, onError, maxReconnectAttempts, reconnectInterval]);

  // URL 变化时重新初始化
  useEffect(() => {
    reconnectAttemptsRef.current = 0;
    initPlayer();
    return () => destroyPlayer();
  }, [initPlayer, destroyPlayer]);

  // 页面可见性变化和焦点恢复时重连（针对移动设备后台切换）
  useEffect(() => {
    const handleVisibilityChange = () => {
      if (document.visibilityState === 'visible' && playerRef.current) {
        console.log('页面恢复可见，检查播放状态...');
        // 检查视频是否还在正常播放
        const video = videoRef.current;
        if (video && (video.paused || video.ended || video.readyState < 3)) {
          console.log('检测到播放中断，尝试重连...');
          reconnectAttemptsRef.current = 0;
          initPlayer(true);
        }
      }
    };

    const handleFocus = () => {
      console.log('窗口获得焦点，检查播放状态...');
      // 延迟检查，给浏览器一些时间恢复
      setTimeout(() => {
        const video = videoRef.current;
        if (video && playerRef.current) {
          // 检查是否需要重连
          if (video.paused || video.ended || video.readyState < 3) {
            console.log('检测到播放中断，尝试重连...');
            reconnectAttemptsRef.current = 0;
            initPlayer(true);
          } else {
            // 尝试继续播放
            const playResult = playerRef.current.play();
            if (playResult && typeof playResult.catch === 'function') {
              playResult.catch(() => {
                console.log('播放失败，尝试重连...');
                reconnectAttemptsRef.current = 0;
                initPlayer(true);
              });
            }
          }
        }
      }, 300);
    };

    const handleOnline = () => {
      console.log('网络恢复，尝试重连...');
      reconnectAttemptsRef.current = 0;
      initPlayer(true);
    };

    document.addEventListener('visibilitychange', handleVisibilityChange);
    window.addEventListener('focus', handleFocus);
    window.addEventListener('online', handleOnline);

    return () => {
      document.removeEventListener('visibilitychange', handleVisibilityChange);
      window.removeEventListener('focus', handleFocus);
      window.removeEventListener('online', handleOnline);
    };
  }, [initPlayer]);

  // 视频事件处理
  const handlePlay = () => {
    setIsPlaying(true);
    setIsLoading(false);
    onPlay?.();
  };

  const handlePause = () => {
    setIsPlaying(false);
    onPause?.();
  };

  const handleError = () => {
    const err = new Error('视频加载失败');
    setError(err.message);
    setIsLoading(false);
    onError?.(err);
  };

  // 手动播放/暂停
  const togglePlay = () => {
    if (!playerRef.current || !videoRef.current) return;
    if (isPlaying) {
      playerRef.current.pause();
    } else {
      playerRef.current.play();
    }
  };

  // 切换静音
  const toggleMute = () => {
    const newMuted = !isMuted;
    setIsMuted(newMuted);
    if (videoRef.current) {
      videoRef.current.muted = newMuted;
    }
  };

  // 调整音量
  const handleVolumeChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const newVolume = parseFloat(e.target.value);
    setVolume(newVolume);
    if (videoRef.current) {
      videoRef.current.volume = newVolume;
    }
    if (newVolume > 0 && isMuted) {
      setIsMuted(false);
      if (videoRef.current) {
        videoRef.current.muted = false;
      }
    }
  };

  // 全屏切换
  const toggleFullscreen = async () => {
    if (!containerRef.current) return;
    try {
      if (!document.fullscreenElement) {
        await containerRef.current.requestFullscreen();
      } else {
        await document.exitFullscreen();
      }
    } catch (err) {
      console.error('全屏切换失败:', err);
    }
  };

  // 监听全屏状态变化
  useEffect(() => {
    const handleFullscreenChange = () => {
      setIsFullscreen(!!document.fullscreenElement);
    };
    document.addEventListener('fullscreenchange', handleFullscreenChange);
    return () => {
      document.removeEventListener('fullscreenchange', handleFullscreenChange);
    };
  }, []);

  // 控制栏自动隐藏
  const resetHideControlsTimer = useCallback(() => {
    setShowControls(true);
    if (hideControlsTimerRef.current) {
      clearTimeout(hideControlsTimerRef.current);
    }
    hideControlsTimerRef.current = window.setTimeout(() => {
      if (isPlaying) {
        setShowControls(false);
      }
    }, 3000);
  }, [isPlaying]);

  const handleMouseMove = () => {
    resetHideControlsTimer();
  };

  const handleMouseLeave = () => {
    if (isPlaying) {
      setShowControls(false);
    }
  };

  // 重试播放
  const retry = () => {
    setError(null);
    reconnectAttemptsRef.current = 0;
    initPlayer();
  };

  return (
    <div 
      ref={containerRef}
      className="relative bg-black rounded-lg overflow-hidden" 
      style={{ width, height }}
      onMouseMove={handleMouseMove}
      onMouseLeave={handleMouseLeave}
    >
      {/* 视频元素 */}
      <video
        ref={videoRef}
        className="w-full h-full object-contain cursor-pointer"
        muted={isMuted}
        playsInline
        onPlay={handlePlay}
        onPause={handlePause}
        onError={handleError}
      />

      {/* 加载中覆盖层 */}
      {isLoading && !error && (
        <div className="absolute inset-0 flex items-center justify-center bg-black/50 pointer-events-none">
          <div className="flex flex-col items-center gap-3">
            <div className="w-10 h-10 border-4 border-blue-500 border-t-transparent rounded-full animate-spin" />
            <span className="text-white text-sm">
              {isReconnecting ? '正在重新连接...' : '正在连接直播流...'}
            </span>
            {isReconnecting && reconnectAttemptsRef.current > 0 && (
              <span className="text-gray-400 text-xs">
                重试 {reconnectAttemptsRef.current}/{maxReconnectAttempts}
              </span>
            )}
          </div>
        </div>
      )}

      {/* 错误覆盖层 */}
      {error && (
        <div className="absolute inset-0 flex items-center justify-center bg-black/80">
          <div className="flex flex-col items-center gap-4 text-center px-4">
            <svg
              className="w-12 h-12 text-red-500"
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
            <p className="text-white">{error}</p>
            <button
              onClick={retry}
              className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors"
            >
              重试
            </button>
          </div>
        </div>
      )}

      {/* 播放/暂停中央按钮 */}
      {!isLoading && !error && !isPlaying && (
        <div
          className="absolute inset-0 flex items-center justify-center cursor-pointer"
          onClick={togglePlay}
        >
          <div className="w-16 h-16 bg-black/50 rounded-full flex items-center justify-center">
            <svg className="w-8 h-8 text-white ml-1" fill="currentColor" viewBox="0 0 24 24">
              <path d="M8 5v14l11-7z" />
            </svg>
          </div>
        </div>
      )}

      {/* 自定义控制栏 */}
      {!error && (
        <div 
          className={`absolute bottom-0 left-0 right-0 p-3 bg-gradient-to-t from-black/80 to-transparent transition-opacity duration-300 ${
            showControls ? 'opacity-100' : 'opacity-0'
          }`}
        >
          <div className="flex items-center justify-between gap-4">
            {/* 左侧：播放/暂停 */}
            <div className="flex items-center gap-2">
              <button
                onClick={togglePlay}
                className="p-2 text-white hover:text-blue-400 transition-colors"
                title={isPlaying ? '暂停' : '播放'}
              >
                {isPlaying ? (
                  <svg className="w-6 h-6" fill="currentColor" viewBox="0 0 24 24">
                    <path d="M6 4h4v16H6V4zm8 0h4v16h-4V4z" />
                  </svg>
                ) : (
                  <svg className="w-6 h-6" fill="currentColor" viewBox="0 0 24 24">
                    <path d="M8 5v14l11-7z" />
                  </svg>
                )}
              </button>
            </div>

            {/* 右侧：音量、全屏 */}
            <div className="flex items-center gap-4">
              {/* 音量控制 */}
              <div className="flex items-center group/volume">
                <button
                  onClick={toggleMute}
                  className="p-2 text-white hover:text-blue-400 transition-colors"
                  title={isMuted ? '取消静音' : '静音'}
                >
                  {isMuted || volume === 0 ? (
                    <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5.586 15H4a1 1 0 01-1-1v-4a1 1 0 011-1h1.586l4.707-4.707C10.923 3.663 12 4.109 12 5v14c0 .891-1.077 1.337-1.707.707L5.586 15z" />
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2" />
                    </svg>
                  ) : volume < 0.5 ? (
                    <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15.536 8.464a5 5 0 010 7.072M5.586 15H4a1 1 0 01-1-1v-4a1 1 0 011-1h1.586l4.707-4.707C10.923 3.663 12 4.109 12 5v14c0 .891-1.077 1.337-1.707.707L5.586 15z" />
                    </svg>
                  ) : (
                    <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15.536 8.464a5 5 0 010 7.072m2.828-9.9a9 9 0 010 12.728M5.586 15H4a1 1 0 01-1-1v-4a1 1 0 011-1h1.586l4.707-4.707C10.923 3.663 12 4.109 12 5v14c0 .891-1.077 1.337-1.707.707L5.586 15z" />
                    </svg>
                  )}
                </button>
                {/* 音量指示点 - 未hover时显示 */}
                <div className="w-3 h-3 bg-white rounded-full group-hover/volume:hidden" />
                {/* 音量滑块 - hover时显示 */}
                <input
                  type="range"
                  min="0"
                  max="1"
                  step="0.05"
                  value={isMuted ? 0 : volume}
                  onChange={handleVolumeChange}
                  className="hidden group-hover/volume:block w-20 h-1 accent-blue-500 cursor-pointer appearance-none bg-gray-600 rounded-full [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-3 [&::-webkit-slider-thumb]:h-3 [&::-webkit-slider-thumb]:bg-white [&::-webkit-slider-thumb]:rounded-full"
                />
              </div>

              {/* 全屏按钮 */}
              <button
                onClick={toggleFullscreen}
                className="p-2 text-white hover:text-blue-400 transition-colors"
                title={isFullscreen ? '退出全屏' : '全屏'}
              >
                {isFullscreen ? (
                  <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 9V4.5M9 9H4.5M9 9L3.75 3.75M9 15v4.5M9 15H4.5M9 15l-5.25 5.25M15 9h4.5M15 9V4.5M15 9l5.25-5.25M15 15h4.5M15 15v4.5m0-4.5l5.25 5.25" />
                  </svg>
                ) : (
                  <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3.75 3.75v4.5m0-4.5h4.5m-4.5 0L9 9M3.75 20.25v-4.5m0 4.5h4.5m-4.5 0L9 15M20.25 3.75h-4.5m4.5 0v4.5m0-4.5L15 9m5.25 11.25h-4.5m4.5 0v-4.5m0 4.5L15 15" />
                  </svg>
                )}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
