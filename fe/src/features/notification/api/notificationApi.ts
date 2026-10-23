import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import type {
  Announcement,
  AnnouncementTargets,
  NotificationItem,
  NotificationQuery,
  SendAnnouncementPayload,
} from '../types';

const get = async <T>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;
const post = async <T>(url: string, body?: unknown): Promise<T> =>
  (await apiClient.post<ApiResponse<T>>(url, body)).data.data;

const INBOX = '/api/v1/common/notifications';

export const notificationApi = {
  list: (q: NotificationQuery) => get<Paginated<NotificationItem>>(INBOX, q),
  unreadCount: async () => (await get<{ count: number }>(`${INBOX}/unread-count`)).count,
  markRead: (id: number) => post<null>(`${INBOX}/${id}/read`),
  markAllRead: () => post<null>(`${INBOX}/read-all`),

  sent: (role: string, q: object) =>
    get<Paginated<Announcement>>(`/api/v1/${role}/announcements`, q),
  targets: (role: string) => get<AnnouncementTargets>(`/api/v1/${role}/announcements/targets`),
  send: (role: string, body: SendAnnouncementPayload) =>
    post<Announcement>(`/api/v1/${role}/announcements`, body),
};
