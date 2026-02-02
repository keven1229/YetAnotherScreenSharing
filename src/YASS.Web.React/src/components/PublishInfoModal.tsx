import { useState } from 'react';
import Modal from './Modal';
import { refreshStreamKey, getPublishCredentials as fetchPublishCredentials } from '../services/api';
import { savePublishCredentials as saveToStorage } from '../utils/storage';
import type { PublishCredentials } from '../types';

interface PublishInfoModalProps {
  isOpen: boolean;
  onClose: () => void;
  roomId: string;
  credentials: PublishCredentials;
  onCredentialsUpdate?: (credentials: PublishCredentials) => void;
}

function CopyButton({ text, label }: { text: string; label: string }) {
  const [copied, setCopied] = useState(false);

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(text);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch (err) {
      console.error('复制失败:', err);
    }
  };

  return (
    <button
      onClick={handleCopy}
      className="flex-shrink-0 px-3 py-1.5 text-sm bg-gray-100 dark:bg-gray-700 hover:bg-gray-200 dark:hover:bg-gray-600 text-gray-700 dark:text-gray-300 rounded-lg transition-colors"
      title={`复制${label}`}
    >
      {copied ? (
        <span className="inline-flex items-center gap-1 text-green-600 dark:text-green-400">
          <svg className="w-4 h-4" fill="currentColor" viewBox="0 0 20 20">
            <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
          </svg>
          已复制
        </span>
      ) : (
        <span className="inline-flex items-center gap-1">
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z" />
          </svg>
          复制
        </span>
      )}
    </button>
  );
}

function InfoField({ label, value }: { label: string; value: string }) {
  return (
    <div className="space-y-1">
      <label className="block text-sm font-medium text-gray-500 dark:text-gray-400">
        {label}
      </label>
      <div className="flex items-center gap-2">
        <input
          type="text"
          value={value}
          readOnly
          className="flex-1 px-3 py-2 bg-gray-50 dark:bg-gray-900 border border-gray-200 dark:border-gray-700 rounded-lg text-gray-900 dark:text-white text-sm font-mono"
        />
        <CopyButton text={value} label={label} />
      </div>
    </div>
  );
}

export default function PublishInfoModal({
  isOpen,
  onClose,
  roomId,
  credentials: initialCredentials,
  onCredentialsUpdate,
}: PublishInfoModalProps) {
  const [credentials, setCredentials] = useState<PublishCredentials>(initialCredentials);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleRefreshKey = async () => {
    setIsRefreshing(true);
    setError(null);

    try {
      // 刷新推流密钥
      await refreshStreamKey(roomId);
      
      // 获取新的凭证
      const newCredentials = await fetchPublishCredentials(roomId);
      
      // 更新本地存储
      saveToStorage(roomId, newCredentials);
      
      // 更新状态
      setCredentials(newCredentials);
      onCredentialsUpdate?.(newCredentials);
    } catch (err) {
      setError(err instanceof Error ? err.message : '刷新失败');
    } finally {
      setIsRefreshing(false);
    }
  };

  // 格式化过期时间
  const formatExpiry = (dateStr: string) => {
    try {
      const date = new Date(dateStr);
      return date.toLocaleString('zh-CN', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
      });
    } catch {
      return dateStr;
    }
  };

  // 检查是否已过期
  const isExpired = new Date(credentials.expiresAt) < new Date();

  return (
    <Modal isOpen={isOpen} onClose={onClose} title="推流信息" size="lg">
      <div className="space-y-4">
        {/* 过期警告 */}
        {isExpired && (
          <div className="flex items-center gap-2 p-3 bg-yellow-50 dark:bg-yellow-900/30 text-yellow-700 dark:text-yellow-400 rounded-lg text-sm">
            <svg className="w-5 h-5 flex-shrink-0" fill="currentColor" viewBox="0 0 20 20">
              <path fillRule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z" clipRule="evenodd" />
            </svg>
            <span>推流密钥已过期，请刷新获取新密钥</span>
          </div>
        )}

        {/* 推流信息字段 */}
        <InfoField label="RTMP 服务器" value={credentials.rtmpUrl} />
        <InfoField label="推流密钥" value={credentials.streamKey} />
        <InfoField label="完整推流 URL" value={credentials.fullPublishUrl} />

        {/* 过期时间 */}
        <div className="flex items-center justify-between text-sm">
          <span className="text-gray-500 dark:text-gray-400">有效期至</span>
          <span className={isExpired ? 'text-red-500' : 'text-gray-700 dark:text-gray-300'}>
            {formatExpiry(credentials.expiresAt)}
          </span>
        </div>

        {/* 错误提示 */}
        {error && (
          <div className="flex items-center gap-2 p-3 bg-red-50 dark:bg-red-900/30 text-red-600 dark:text-red-400 rounded-lg text-sm">
            <svg className="w-5 h-5 flex-shrink-0" fill="currentColor" viewBox="0 0 20 20">
              <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clipRule="evenodd" />
            </svg>
            <span>{error}</span>
          </div>
        )}

        {/* 刷新按钮 */}
        <div className="pt-2 border-t border-gray-200 dark:border-gray-700">
          <button
            onClick={handleRefreshKey}
            disabled={isRefreshing}
            className="inline-flex items-center gap-2 px-4 py-2 text-sm bg-gray-100 dark:bg-gray-700 hover:bg-gray-200 dark:hover:bg-gray-600 text-gray-700 dark:text-gray-300 rounded-lg transition-colors disabled:opacity-50"
          >
            <svg
              className={`w-4 h-4 ${isRefreshing ? 'animate-spin' : ''}`}
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            {isRefreshing ? '刷新中...' : '刷新推流密钥'}
          </button>
          <p className="mt-2 text-xs text-gray-500 dark:text-gray-400">
            刷新后旧密钥将失效，OBS 需重新配置
          </p>
        </div>

        {/* OBS 配置说明 */}
        <div className="mt-4 p-4 bg-gray-50 dark:bg-gray-900 rounded-lg">
          <h4 className="text-sm font-medium text-gray-900 dark:text-white mb-2">
            OBS 配置说明
          </h4>
          <ol className="text-sm text-gray-600 dark:text-gray-400 space-y-1 list-decimal list-inside">
            <li>打开 OBS，点击「设置」→「推流」</li>
            <li>服务选择「自定义」</li>
            <li>将上方「RTMP 服务器」地址填入「服务器」</li>
            <li>将上方「推流密钥」填入「推流码」</li>
            <li>点击「确定」保存，然后开始推流</li>
          </ol>
        </div>
      </div>
    </Modal>
  );
}
