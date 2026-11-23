import { Tag } from 'antd';
import { StatusTag } from '@/components/common/StatusTag';
import { NOTIFICATION_SOURCE_LABEL, NOTIFICATION_URGENT_LABEL } from '../constants';
import type { NotificationItem } from '../types';

/** Nhãn màu của thông báo: loại (từ quản lý, đơn từ, kho, sản xuất, hệ thống) và "Khẩn" (đỏ). */
export function NotificationTypeTags({
  n,
}: {
  n: Pick<NotificationItem, 'sourceType' | 'isUrgent'>;
}) {
  return (
    <>
      <StatusTag status={n.sourceType} labels={NOTIFICATION_SOURCE_LABEL} />
      {n.isUrgent && (
        <Tag color={NOTIFICATION_URGENT_LABEL.color}>{NOTIFICATION_URGENT_LABEL.text}</Tag>
      )}
    </>
  );
}
