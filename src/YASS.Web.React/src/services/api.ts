import axios, { AxiosInstance } from 'axios';
import type {
  ApiResponse,
  RoomInfo,
  RoomListResponse,
  PlaybackUrls,
  CreateRoomRequest,
  CreateRoomResponse,
  PublishCredentials,
} from '../types';

// API 基础地址
const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000';

/**
 * 获取 API 基础地址（用于构建静态资源URL）
 */
export function getApiBaseUrl(): string {
  return API_BASE_URL;
}

// 创建 axios 实例
const apiClient: AxiosInstance = axios.create({
  baseURL: API_BASE_URL,
  timeout: 10000,
  headers: {
    'Content-Type': 'application/json',
  },
});

// 响应拦截器 - 统一处理错误
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    console.error('API Error:', error);
    return Promise.reject(error);
  }
);

/**
 * 解包 API 响应，提取 data 或抛出错误
 */
function unwrapResponse<T>(response: ApiResponse<T>): T {
  if (response.success && response.data !== undefined) {
    return response.data;
  }
  throw new Error(response.error || '请求失败');
}

// ==================== Room API ====================

/**
 * 获取房间列表
 */
export async function getRooms(page = 1, pageSize = 20): Promise<RoomListResponse> {
  const response = await apiClient.get<ApiResponse<RoomListResponse>>('/api/rooms', {
    params: { page, pageSize },
  });
  return unwrapResponse(response.data);
}

/**
 * 获取房间详情
 */
export async function getRoom(roomId: string): Promise<RoomInfo> {
  const response = await apiClient.get<ApiResponse<RoomInfo>>(`/api/rooms/${roomId}`);
  return unwrapResponse(response.data);
}

/**
 * 获取房间播放地址
 */
export async function getPlaybackUrls(roomId: string): Promise<PlaybackUrls> {
  const response = await apiClient.get<ApiResponse<PlaybackUrls>>(
    `/api/rooms/${roomId}/playback`
  );
  return unwrapResponse(response.data);
}

/**
 * 创建房间
 */
export async function createRoom(request: CreateRoomRequest): Promise<CreateRoomResponse> {
  const response = await apiClient.post<ApiResponse<CreateRoomResponse>>(
    '/api/rooms',
    request
  );
  return unwrapResponse(response.data);
}

/**
 * 删除房间
 */
export async function deleteRoom(roomId: string): Promise<void> {
  const response = await apiClient.delete<ApiResponse<null>>(`/api/rooms/${roomId}`);
  if (!response.data.success) {
    throw new Error(response.data.error || '删除失败');
  }
}

/**
 * 刷新推流密钥
 */
export async function refreshStreamKey(roomId: string): Promise<void> {
  const response = await apiClient.post<ApiResponse<null>>(
    `/api/rooms/${roomId}/refresh-key`
  );
  if (!response.data.success) {
    throw new Error(response.data.error || '刷新失败');
  }
}

/**
 * 获取推流凭证
 */
export async function getPublishCredentials(roomId: string): Promise<PublishCredentials> {
  const response = await apiClient.get<ApiResponse<PublishCredentials>>(
    `/api/rooms/${roomId}/publish`
  );
  return unwrapResponse(response.data);
}

export { apiClient };
