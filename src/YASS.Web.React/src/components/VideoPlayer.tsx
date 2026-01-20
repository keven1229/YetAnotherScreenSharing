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
}: VideoPlayerProps) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const playerRef = useRef<mpegts.Player | null>(null);
  const [isPlaying, setIsPlaying] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  // 销毁播放器
  const destroyPlayer = useCallback(() => {
    if (playerRef.current) {
      playerRef.current.pause();
      playerRef.current.unload();
      playerRef.current.detachMediaElement();
      playerRef.current.destroy();
      playerRef.current = null;
    }
  }, []);

  // 初始化播放器
  const initPlayer = useCallback(() => {
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

    try {
      // 创建播放器
      const player = mpegts.createPlayer(
        {
          type: 'flv',
          url: url,
          isLive: true,
          hasAudio: false,
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
        const err = new Error(`播放错误: ${errorDetail}`);
        setError(err.message);
        setIsLoading(false);
        onError?.(err);
      });

      player.on(mpegts.Events.LOADING_COMPLETE, () => {
        console.log('Loading complete');
      });

      player.on(mpegts.Events.METADATA_ARRIVED, () => {
        setIsLoading(false);
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
      onError?.(error);
    }
  }, [url, autoPlay, destroyPlayer, onError]);

  // URL 变化时重新初始化
  useEffect(() => {
    initPlayer();
    return () => destroyPlayer();
  }, [initPlayer, destroyPlayer]);

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

  // 重试播放
  const retry = () => {
    setError(null);
    initPlayer();
  };

  return (
    <div className="relative bg-black rounded-lg overflow-hidden" style={{ width, height }}>
      {/* 视频元素 */}
      <video
        ref={videoRef}
        className="w-full h-full object-contain"
        muted={muted}
        playsInline
        onPlay={handlePlay}
        onPause={handlePause}
        onError={handleError}
        onClick={togglePlay}
      />

      {/* 加载中覆盖层 */}
      {isLoading && !error && (
        <div className="absolute inset-0 flex items-center justify-center bg-black/50">
          <div className="flex flex-col items-center gap-3">
            <div className="w-10 h-10 border-4 border-blue-500 border-t-transparent rounded-full animate-spin" />
            <span className="text-white text-sm">正在连接直播流...</span>
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

      {/* 播放/暂停指示器 */}
      {!isLoading && !error && !isPlaying && (
        <div
          className="absolute inset-0 flex items-center justify-center bg-black/30 cursor-pointer"
          onClick={togglePlay}
        >
          <svg
            className="w-16 h-16 text-white/80 hover:text-white transition-colors"
            fill="currentColor"
            viewBox="0 0 24 24"
          >
            <path d="M8 5v14l11-7z" />
          </svg>
        </div>
      )}
    </div>
  );
}
