import { useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { NOTIFICATION_REF } from '../constants';
import type { NotificationItem } from '../types';
import { useNotificationModal } from './useNotificationModal';

/** Thông báo có hành động khi bấm: mở cửa sổ chi tiết tại chỗ hoặc chuyển tới màn liên quan. */
export const hasNotificationAction = (
  n: Pick<NotificationItem, 'refTable' | 'refId' | 'linkUrl'>,
): boolean =>
  (n.refTable === NOTIFICATION_REF.KPI_MISMATCH && n.refId !== null) || Boolean(n.linkUrl);

/**
 * Bấm một thông báo: loại có cửa sổ chi tiết (lệch kiểm cuối đơn) thì mở cửa sổ ngay tại chỗ, người dùng vẫn ở màn đang đứng; loại còn
 * lại có link thì chuyển tới màn đó. Trả true nếu thông báo có hành động (để chuông biết đóng danh sách).
 */
export function useOpenNotification() {
  const navigate = useNavigate();
  const openMismatch = useNotificationModal((s) => s.openMismatch);

  return useCallback(
    (n: Pick<NotificationItem, 'refTable' | 'refId' | 'linkUrl'>): boolean => {
      if (n.refTable === NOTIFICATION_REF.KPI_MISMATCH && n.refId !== null) {
        openMismatch(n.refId);
        return true;
      }
      if (n.linkUrl) {
        navigate(n.linkUrl);
        return true;
      }
      return false;
    },
    [navigate, openMismatch],
  );
}
