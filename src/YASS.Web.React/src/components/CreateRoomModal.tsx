import { useState } from 'react';
import Modal from './Modal';
import { createRoom } from '../services/api';
import { savePublishCredentials } from '../utils/storage';
import type { PublishCredentials } from '../types';

interface CreateRoomModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: (roomId: string, credentials: PublishCredentials) => void;
}

export default function CreateRoomModal({ isOpen, onClose, onSuccess }: CreateRoomModalProps) {
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) {
      setError('请输入房间名称');
      return;
    }

    setIsLoading(true);
    setError(null);

    try {
      const response = await createRoom({
        name: name.trim(),
        description: description.trim() || undefined,
      });

      // 保存推流凭证到 localStorage
      savePublishCredentials(response.room.id, response.publishCredentials);

      // 重置表单
      setName('');
      setDescription('');

      // 通知父组件
      onSuccess(response.room.id, response.publishCredentials);
    } catch (err) {
      setError(err instanceof Error ? err.message : '创建房间失败');
    } finally {
      setIsLoading(false);
    }
  };

  const handleClose = () => {
    if (!isLoading) {
      setName('');
      setDescription('');
      setError(null);
      onClose();
    }
  };

  return (
    <Modal isOpen={isOpen} onClose={handleClose} title="创建房间">
      <form onSubmit={handleSubmit} className="space-y-4">
        {/* 房间名称 */}
        <div>
          <label
            htmlFor="roomName"
            className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1"
          >
            房间名称 <span className="text-red-500">*</span>
          </label>
          <input
            type="text"
            id="roomName"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="输入房间名称"
            disabled={isLoading}
            className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent disabled:opacity-50"
          />
        </div>

        {/* 房间描述 */}
        <div>
          <label
            htmlFor="roomDescription"
            className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1"
          >
            房间描述（可选）
          </label>
          <textarea
            id="roomDescription"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            placeholder="输入房间描述"
            rows={3}
            disabled={isLoading}
            className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent disabled:opacity-50 resize-none"
          />
        </div>

        {/* 错误提示 */}
        {error && (
          <div className="flex items-center gap-2 p-3 bg-red-50 dark:bg-red-900/30 text-red-600 dark:text-red-400 rounded-lg text-sm">
            <svg className="w-5 h-5 flex-shrink-0" fill="currentColor" viewBox="0 0 20 20">
              <path
                fillRule="evenodd"
                d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z"
                clipRule="evenodd"
              />
            </svg>
            <span>{error}</span>
          </div>
        )}

        {/* 按钮 */}
        <div className="flex justify-end gap-3 pt-2">
          <button
            type="button"
            onClick={handleClose}
            disabled={isLoading}
            className="px-4 py-2 text-gray-700 dark:text-gray-300 bg-gray-100 dark:bg-gray-700 hover:bg-gray-200 dark:hover:bg-gray-600 rounded-lg transition-colors disabled:opacity-50"
          >
            取消
          </button>
          <button
            type="submit"
            disabled={isLoading}
            className="inline-flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-400 text-white rounded-lg transition-colors"
          >
            {isLoading && (
              <svg className="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
              </svg>
            )}
            创建房间
          </button>
        </div>
      </form>
    </Modal>
  );
}
