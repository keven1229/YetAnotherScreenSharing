import type { PublishCredentials } from '../types';

const STORAGE_KEY_PREFIX = 'yass_publish_';

/**
 * 保存推流凭证到 localStorage
 */
export function savePublishCredentials(roomId: string, credentials: PublishCredentials): void {
  const key = `${STORAGE_KEY_PREFIX}${roomId}`;
  localStorage.setItem(key, JSON.stringify(credentials));
}

/**
 * 从 localStorage 获取推流凭证
 */
export function getPublishCredentials(roomId: string): PublishCredentials | null {
  const key = `${STORAGE_KEY_PREFIX}${roomId}`;
  const data = localStorage.getItem(key);
  if (!data) return null;
  try {
    return JSON.parse(data) as PublishCredentials;
  } catch {
    return null;
  }
}

/**
 * 从 localStorage 移除推流凭证
 */
export function removePublishCredentials(roomId: string): void {
  const key = `${STORAGE_KEY_PREFIX}${roomId}`;
  localStorage.removeItem(key);
}

/**
 * 获取所有已保存的房间 ID 列表
 */
export function getSavedRoomIds(): string[] {
  const ids: string[] = [];
  for (let i = 0; i < localStorage.length; i++) {
    const key = localStorage.key(i);
    if (key?.startsWith(STORAGE_KEY_PREFIX)) {
      ids.push(key.substring(STORAGE_KEY_PREFIX.length));
    }
  }
  return ids;
}

/**
 * 检查房间是否有本地保存的推流凭证
 */
export function hasLocalCredentials(roomId: string): boolean {
  return getPublishCredentials(roomId) !== null;
}
