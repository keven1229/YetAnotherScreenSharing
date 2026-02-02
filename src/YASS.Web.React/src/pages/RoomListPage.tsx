import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { getRooms, deleteRoom, getApiBaseUrl } from '../services/api';
import { RoomStatus, RoomStatusInfo, type RoomInfo, type PublishCredentials } from '../types';
import { hasLocalCredentials, getPublishCredentials, removePublishCredentials } from '../utils/storage';
import CreateRoomModal from '../components/CreateRoomModal';
import PublishInfoModal from '../components/PublishInfoModal';

function RoomCard({ room, onShowPublishInfo, onDelete }: { room: RoomInfo; onShowPublishInfo?: (roomId: string) => void; onDelete?: (room: RoomInfo) => void }) {
  const statusInfo = RoomStatusInfo[room.status];
  const isLive = room.status === RoomStatus.Live;
  const hasCredentials = hasLocalCredentials(room.id);
  const [imageError, setImageError] = useState(false);

  // 构建预览图URL，添加时间戳防止缓存
  const thumbnailUrl = room.thumbnailUrl && !room.isPrivacyMode
    ? `${getApiBaseUrl()}${room.thumbnailUrl}?t=${room.thumbnailUpdatedAt || Date.now()}`
    : null;

  const handlePublishInfoClick = (e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();
    onShowPublishInfo?.(room.id);
  };

  const handleDeleteClick = (e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();
    onDelete?.(room);
  };

  return (
    <div className="relative">
      <Link
        to={`/room/${room.id}`}
        className="block bg-white dark:bg-gray-800 rounded-xl shadow-md hover:shadow-lg transition-shadow overflow-hidden"
      >
      {/* 缩略图区域 */}
      <div className="relative aspect-video bg-gray-200 dark:bg-gray-700">
        {/* 显示预览图（如果存在且未启用隐私模式） */}
        {thumbnailUrl && !imageError ? (
          <img
            src={thumbnailUrl}
            alt={`${room.name} 预览`}
            className="absolute inset-0 w-full h-full object-cover"
            loading="lazy"
            onError={() => setImageError(true)}
          />
        ) : isLive ? (
          <div className="absolute inset-0 flex items-center justify-center">
            <div className="text-center">
              <svg
                className="w-12 h-12 mx-auto text-green-500 animate-pulse"
                fill="currentColor"
                viewBox="0 0 24 24"
              >
                <circle cx="12" cy="12" r="10" opacity="0.3" />
                <circle cx="12" cy="12" r="6" />
              </svg>
              <p className="mt-2 text-sm text-gray-600 dark:text-gray-300">直播中</p>
            </div>
          </div>
        ) : (
          <div className="absolute inset-0 flex items-center justify-center">
            <svg
              className="w-12 h-12 text-gray-400"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={1.5}
                d="M15 10l4.553-2.276A1 1 0 0121 8.618v6.764a1 1 0 01-1.447.894L15 14M5 18h8a2 2 0 002-2V8a2 2 0 00-2-2H5a2 2 0 00-2 2v8a2 2 0 002 2z"
              />
            </svg>
          </div>
        )}

        {/* 隐私模式标识 */}
        {room.isPrivacyMode && (
          <div className="absolute top-2 right-2">
            <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium text-white bg-gray-600">
              <svg className="w-3 h-3 mr-1" fill="currentColor" viewBox="0 0 24 24">
                <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z" />
              </svg>
              隐私
            </span>
          </div>
        )}

        {/* 状态标签 */}
        <div className="absolute top-2 left-2">
          <span
            className={`inline-flex items-center px-2 py-1 rounded-full text-xs font-medium text-white ${statusInfo.color}`}
          >
            {isLive && (
              <span className="w-2 h-2 mr-1 bg-white rounded-full animate-pulse" />
            )}
            {statusInfo.label}
          </span>
        </div>

        {/* 观众数 */}
        {isLive && (
          <div className="absolute bottom-2 right-2 flex items-center gap-1 px-2 py-1 bg-black/60 rounded-full">
            <svg
              className="w-4 h-4 text-white"
              fill="currentColor"
              viewBox="0 0 24 24"
            >
              <path d="M12 4.5C7 4.5 2.73 7.61 1 12c1.73 4.39 6 7.5 11 7.5s9.27-3.11 11-7.5c-1.73-4.39-6-7.5-11-7.5zM12 17c-2.76 0-5-2.24-5-5s2.24-5 5-5 5 2.24 5 5-2.24 5-5 5zm0-8c-1.66 0-3 1.34-3 3s1.34 3 3 3 3-1.34 3-3-1.34-3-3-3z" />
            </svg>
            <span className="text-xs text-white">{room.viewerCount}</span>
          </div>
        )}
      </div>

      {/* 信息区域 */}
      <div className="p-4">
        <h3 className="font-semibold text-gray-900 dark:text-white truncate">
          {room.name}
        </h3>
        {room.description && (
          <p className="mt-1 text-sm text-gray-500 dark:text-gray-400 line-clamp-2">
            {room.description}
          </p>
        )}
        <div className="mt-2 flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400">
          <svg className="w-4 h-4" fill="currentColor" viewBox="0 0 24 24">
            <path d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z" />
          </svg>
          <span>{room.ownerName}</span>
        </div>
      </div>
    </Link>
      {/* 推流信息按钮 */}
      {hasCredentials && (
        <button
          onClick={handlePublishInfoClick}
          className="absolute top-2 right-12 p-1.5 bg-white/90 dark:bg-gray-800/90 hover:bg-white dark:hover:bg-gray-800 rounded-lg shadow-md transition-colors z-10"
          title="查看推流信息"
        >
          <svg className="w-4 h-4 text-blue-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 10l4.553-2.276A1 1 0 0121 8.618v6.764a1 1 0 01-1.447.894L15 14M5 18h8a2 2 0 002-2V8a2 2 0 00-2-2H5a2 2 0 00-2 2v8a2 2 0 002 2z" />
          </svg>
        </button>
      )}
      {/* 删除按钮 */}
      {hasCredentials && (
        <button
          onClick={handleDeleteClick}
          className="absolute top-2 right-2 p-1.5 bg-white/90 dark:bg-gray-800/90 hover:bg-red-100 dark:hover:bg-red-900/50 rounded-lg shadow-md transition-colors z-10"
          title="删除房间"
        >
          <svg className="w-4 h-4 text-red-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
          </svg>
        </button>
      )}
    </div>
  );
}

export default function RoomListPage() {
  const queryClient = useQueryClient();
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [deleteConfirm, setDeleteConfirm] = useState<{ isOpen: boolean; room: RoomInfo | null; isDeleting: boolean }>({ isOpen: false, room: null, isDeleting: false });
  const [publishInfoModal, setPublishInfoModal] = useState<{
    isOpen: boolean;
    roomId: string;
    credentials: PublishCredentials | null;
  }>({ isOpen: false, roomId: '', credentials: null });

  const {
    data,
    isLoading,
    isError,
    error,
    refetch,
    isFetching,
  } = useQuery({
    queryKey: ['rooms'],
    queryFn: () => getRooms(1, 50),
    refetchInterval: 30000, // 30秒自动刷新
  });

  const handleCreateSuccess = (_roomId: string, credentials: PublishCredentials) => {
    setIsCreateModalOpen(false);
    setPublishInfoModal({ isOpen: true, roomId: credentials.roomId, credentials });
    refetch();
  };

  const handleShowPublishInfo = (roomId: string) => {
    const credentials = getPublishCredentials(roomId);
    if (credentials) {
      setPublishInfoModal({ isOpen: true, roomId, credentials });
    }
  };

  const handleDeleteRoom = (room: RoomInfo) => {
    setDeleteConfirm({ isOpen: true, room, isDeleting: false });
  };

  const confirmDelete = async () => {
    if (!deleteConfirm.room) return;
    
    setDeleteConfirm(prev => ({ ...prev, isDeleting: true }));
    try {
      await deleteRoom(deleteConfirm.room.id);
      removePublishCredentials(deleteConfirm.room.id);
      queryClient.invalidateQueries({ queryKey: ['rooms'] });
      setDeleteConfirm({ isOpen: false, room: null, isDeleting: false });
    } catch (error) {
      console.error('删除房间失败:', error);
      setDeleteConfirm(prev => ({ ...prev, isDeleting: false }));
      alert(error instanceof Error ? error.message : '删除失败');
    }
  };

  return (
    <div className="min-h-screen bg-gray-100 dark:bg-gray-900">
      {/* 创建房间弹窗 */}
      <CreateRoomModal
        isOpen={isCreateModalOpen}
        onClose={() => setIsCreateModalOpen(false)}
        onSuccess={handleCreateSuccess}
      />

      {/* 推流信息弹窗 */}
      {publishInfoModal.credentials && (
        <PublishInfoModal
          isOpen={publishInfoModal.isOpen}
          onClose={() => setPublishInfoModal({ isOpen: false, roomId: '', credentials: null })}
          roomId={publishInfoModal.roomId}
          credentials={publishInfoModal.credentials}
        />
      )}

      {/* 删除确认弹窗 */}
      {deleteConfirm.isOpen && deleteConfirm.room && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
          <div className="bg-white dark:bg-gray-800 rounded-xl shadow-xl p-6 max-w-md w-full mx-4">
            <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">确认删除</h3>
            <p className="text-gray-600 dark:text-gray-300 mb-6">
              确定要删除房间 "<span className="font-semibold">{deleteConfirm.room.name}</span>" 吗？此操作不可恢复。
            </p>
            <div className="flex justify-end gap-3">
              <button
                onClick={() => setDeleteConfirm({ isOpen: false, room: null, isDeleting: false })}
                disabled={deleteConfirm.isDeleting}
                className="px-4 py-2 text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-lg transition-colors"
              >
                取消
              </button>
              <button
                onClick={confirmDelete}
                disabled={deleteConfirm.isDeleting}
                className="px-4 py-2 bg-red-600 hover:bg-red-700 disabled:bg-red-400 text-white rounded-lg transition-colors"
              >
                {deleteConfirm.isDeleting ? '删除中...' : '删除'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* 头部 */}
      <header className="bg-white dark:bg-gray-800 shadow">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <svg
                className="w-8 h-8 text-blue-600"
                fill="currentColor"
                viewBox="0 0 24 24"
              >
                <path d="M21 3H3c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h18c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm0 16H3V5h18v14zM9 8h2v8H9zm4 0h2v8h-2z" />
              </svg>
              <h1 className="text-xl font-bold text-gray-900 dark:text-white">
                YASS 屏幕共享
              </h1>
            </div>
            <div className="flex items-center gap-3">
              <button
                onClick={() => setIsCreateModalOpen(true)}
                className="inline-flex items-center gap-2 px-4 py-2 bg-green-600 hover:bg-green-700 text-white rounded-lg transition-colors"
              >
                <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                </svg>
                创建房间
              </button>
              <button
                onClick={() => refetch()}
                disabled={isFetching}
                className="inline-flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-400 text-white rounded-lg transition-colors"
              >
                <svg
                  className={`w-4 h-4 ${isFetching ? 'animate-spin' : ''}`}
                  fill="none"
                  stroke="currentColor"
                  viewBox="0 0 24 24"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth={2}
                    d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"
                  />
                </svg>
                刷新
              </button>
            </div>
          </div>
        </div>
      </header>

      {/* 主内容 */}
      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {/* 加载状态 */}
        {isLoading && (
          <div className="flex items-center justify-center py-20">
            <div className="flex flex-col items-center gap-4">
              <div className="w-12 h-12 border-4 border-blue-600 border-t-transparent rounded-full animate-spin" />
              <p className="text-gray-600 dark:text-gray-300">加载房间列表...</p>
            </div>
          </div>
        )}

        {/* 错误状态 */}
        {isError && (
          <div className="flex items-center justify-center py-20">
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
              <h2 className="mt-4 text-lg font-semibold text-gray-900 dark:text-white">
                加载失败
              </h2>
              <p className="mt-2 text-gray-600 dark:text-gray-300">
                {error instanceof Error ? error.message : '未知错误'}
              </p>
              <button
                onClick={() => refetch()}
                className="mt-4 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors"
              >
                重试
              </button>
            </div>
          </div>
        )}

        {/* 房间列表 */}
        {data && (
          <>
            <div className="mb-6 flex items-center justify-between">
              <p className="text-gray-600 dark:text-gray-300">
                共 <span className="font-semibold">{data.totalCount}</span> 个房间
              </p>
            </div>

            {data.rooms.length === 0 ? (
              <div className="text-center py-20">
                <svg
                  className="w-16 h-16 mx-auto text-gray-400"
                  fill="none"
                  stroke="currentColor"
                  viewBox="0 0 24 24"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth={1.5}
                    d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10"
                  />
                </svg>
                <h2 className="mt-4 text-lg font-semibold text-gray-900 dark:text-white">
                  暂无房间
                </h2>
                <p className="mt-2 text-gray-600 dark:text-gray-300">
                  等待主播创建房间并开始直播
                </p>
              </div>
            ) : (
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6">
                {data.rooms.map((room) => (
                  <RoomCard key={room.id} room={room} onShowPublishInfo={handleShowPublishInfo} onDelete={handleDeleteRoom} />
                ))}
              </div>
            )}
          </>
        )}
      </main>
    </div>
  );
}
