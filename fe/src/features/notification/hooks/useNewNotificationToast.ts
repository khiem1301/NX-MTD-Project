import { useQuery, useQueryClient } from '@tanstack/react-query';
import { notification } from 'antd';
import { useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { notificationApi } from '../api/notificationApi';
import { NOTIFICATION_KEY } from './useNotifications';

const POLL_MS = 30_000;
const LATEST = 5;
const TOAST_SECONDS = 8;

/**
 * Hiện toast góc màn hình khi có thông báo chưa đọc mới (nạp lại mỗi 30 giây). Lần tải đầu chỉ ghi nhớ những thông báo
 * đang có để không bật toast cho thông báo cũ; bấm vào toast mở màn liên quan. Cũng làm mới chuông và danh sách.
 */
export function useNewNotificationToast() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const seen = useRef<Set<number> | null>(null);
  const latest = useQuery({
    queryKey: [...NOTIFICATION_KEY, 'toast'],
    queryFn: () => notificationApi.list({ page: 1, pageSize: LATEST, unreadOnly: true }),
    refetchInterval: POLL_MS,
  });
  const items = latest.data?.items;

  useEffect(() => {
    if (!items) return;
    if (seen.current === null) {
      seen.current = new Set(items.map((n) => n.notificationId));
      return;
    }
    const fresh = items.filter((n) => !seen.current!.has(n.notificationId));
    if (fresh.length === 0) return;
    fresh.forEach((n) => {
      seen.current!.add(n.notificationId);
      notification.info({
        key: `n-${n.notificationId}`,
        message: n.title,
        description: n.message,
        duration: TOAST_SECONDS,
        placement: 'bottomRight',
        style: n.linkUrl ? { cursor: 'pointer' } : undefined,
        onClick: () => {
          if (n.linkUrl) navigate(n.linkUrl);
          notification.destroy(`n-${n.notificationId}`);
        },
      });
    });
    void queryClient.invalidateQueries({ queryKey: [...NOTIFICATION_KEY, 'unread-count'] });
    void queryClient.invalidateQueries({ queryKey: [...NOTIFICATION_KEY, 'list'] });
  }, [items, navigate, queryClient]);
}
