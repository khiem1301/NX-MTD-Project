import type { PageParams } from '@/types/api';

export type NotificationItem = {
  notificationId: number;
  sourceType: string;
  title: string;
  message: string;
  linkUrl: string | null;
  isRead: boolean;
  isUrgent: boolean;
  isManual: boolean;
  createdAt: string;
};

export type NotificationGroup = 'MANUAL' | 'AUTO';

export type NotificationQuery = PageParams & { unreadOnly?: boolean; group?: NotificationGroup };

export type Announcement = {
  announcementId: number;
  content: string;
  priority: string;
  targetScope: string;
  senderName: string;
  sentAt: string;
  recipientCount: number;
  targets: string[];
};

export type AnnouncementTargets = {
  teams: { teamId: number; teamName: string; teamType: string }[];
  roles: { roleId: number; roleCode: string; roleName: string }[];
};

export type SendAnnouncementPayload = {
  content: string;
  priority: string;
  targetScope: string;
  teamIds?: number[];
  roleIds?: number[];
};
