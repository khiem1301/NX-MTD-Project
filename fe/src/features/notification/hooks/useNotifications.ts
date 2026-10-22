import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query';
import { useApiMutation } from '@/hooks/useApiMutation';
import { useRolePath } from '@/hooks/useRolePath';
import { notificationApi } from '../api/notificationApi';
import type { NotificationQuery, SendAnnouncementPayload } from '../types';

export const NOTIFICATION_KEY = ['notifications'] as const;
const POLL_MS = 60_000;

/** Số thông báo chưa đọc, tự nạp lại mỗi phút (chưa có push thời gian thực). */
export const useUnreadCount = () =>
  useQuery({
    queryKey: [...NOTIFICATION_KEY, 'unread-count'],
    queryFn: notificationApi.unreadCount,
    refetchInterval: POLL_MS,
  });

export const useNotifications = (query: NotificationQuery, enabled = true) =>
  useQuery({
    queryKey: [...NOTIFICATION_KEY, 'list', query],
    queryFn: () => notificationApi.list(query),
    placeholderData: keepPreviousData,
    enabled,
  });

export const useMarkRead = () =>
  useApiMutation((id: number) => notificationApi.markRead(id), { invalidate: [NOTIFICATION_KEY] });

export const useMarkAllRead = () =>
  useApiMutation(() => notificationApi.markAllRead(), { invalidate: [NOTIFICATION_KEY] });

export const useSentAnnouncements = (query: object) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...NOTIFICATION_KEY, 'sent', role, query],
    queryFn: () => notificationApi.sent(role, query),
    placeholderData: keepPreviousData,
  });
};

export const useAnnouncementTargets = (enabled: boolean) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...NOTIFICATION_KEY, 'targets', role],
    queryFn: () => notificationApi.targets(role),
    enabled,
    staleTime: 60_000,
  });
};

export const useSendAnnouncement = () => {
  const role = useRolePath();
  const queryClient = useQueryClient();
  return useApiMutation(
    async (body: SendAnnouncementPayload) => {
      const result = await notificationApi.send(role, body);
      await queryClient.invalidateQueries({ queryKey: NOTIFICATION_KEY });
      return result;
    },
    { invalidate: [NOTIFICATION_KEY], successMessage: 'Đã gửi thông báo.' },
  );
};
