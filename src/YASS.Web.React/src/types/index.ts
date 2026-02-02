// ==================== Enums ====================

/**
 * 房间状态
 */
export enum RoomStatus {
  /** 等待推流 */
  Waiting = 0,
  /** 正在直播 */
  Live = 1,
  /** 已结束 */
  Ended = 2,
}

/**
 * 视频编码格式
 */
export enum CodecType {
  /** H.264/AVC */
  H264 = 0,
  /** H.265/HEVC */
  H265 = 1,
  /** AV1 */
  AV1 = 2,
}

/**
 * 流协议类型
 */
export enum StreamProtocol {
  RTMP = 0,
  HttpFlv = 1,
  HLS = 2,
}

// ==================== Models ====================

/**
 * 流配置
 */
export interface StreamConfig {
  codec: CodecType;
  width: number;
  height: number;
  frameRate: number;
  videoBitrate: number;
  keyFrameInterval: number;
  preset: string;
}

/**
 * 房间信息
 */
export interface RoomInfo {
  id: string;
  name: string;
  description?: string;
  ownerId: string;
  ownerName: string;
  status: RoomStatus;
  viewerCount: number;
  createdAt: string;
  startedAt?: string;
  lastActiveAt?: string;
  streamConfig?: StreamConfig;
  /** 预览图URL */
  thumbnailUrl?: string;
  /** 预览图更新时间 */
  thumbnailUpdatedAt?: string;
  /** 是否启用隐私模式 */
  isPrivacyMode: boolean;
}

/**
 * 播放地址
 */
export interface PlaybackUrls {
  roomId: string;
  rtmpUrl?: string;
  httpFlvUrl?: string;
  httpFlvH264Url?: string;
  hlsUrl?: string;
  originalCodec: CodecType;
}

/**
 * 推流凭证
 */
export interface PublishCredentials {
  roomId: string;
  streamKey: string;
  rtmpUrl: string;
  fullPublishUrl: string;
  expiresAt: string;
}

// ==================== DTOs ====================

/**
 * 通用 API 响应
 */
export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  error?: string;
  errorCode?: string;
}

/**
 * 创建房间请求
 */
export interface CreateRoomRequest {
  name: string;
  description?: string;
  /** 是否启用隐私模式 */
  isPrivacyMode?: boolean;
}

/**
 * 创建房间响应
 */
export interface CreateRoomResponse {
  room: RoomInfo;
  publishCredentials: PublishCredentials;
}

/**
 * 房间列表响应
 */
export interface RoomListResponse {
  rooms: RoomInfo[];
  totalCount: number;
  page: number;
  pageSize: number;
}

// ==================== Utility Types ====================

/**
 * 房间状态显示信息
 */
export const RoomStatusInfo: Record<RoomStatus, { label: string; color: string }> = {
  [RoomStatus.Waiting]: { label: '等待中', color: 'bg-yellow-500' },
  [RoomStatus.Live]: { label: '直播中', color: 'bg-green-500' },
  [RoomStatus.Ended]: { label: '已结束', color: 'bg-gray-500' },
};
