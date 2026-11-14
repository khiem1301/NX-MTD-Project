import { Suspense } from 'react';
import { KpiMismatchModalSlot } from '@/app/featureSlots';
import { ROLES } from '@/constants/roles';
import { useAuth } from '@/hooks/useAuth';
import { useNotificationModal } from '../hooks/useNotificationModal';

/**
 * Các cửa sổ chi tiết mở từ thông báo, đặt một lần ở thanh trên cùng để mở được từ chuông, toast và hộp thư mà người dùng vẫn ở nguyên màn
 * đang đứng. Lệch kiểm cuối đơn: Quản đốc / Admin xem bằng endpoint xem trước, tổ trưởng / tổ phó bằng endpoint chỉ xem của tổ mình.
 */
export function NotificationModals() {
  const { user } = useAuth();
  const mismatchTargetId = useNotificationModal((s) => s.mismatchTargetId);
  const closeMismatch = useNotificationModal((s) => s.closeMismatch);
  const isManager = user?.role === ROLES.Manager || user?.role === ROLES.Admin;

  if (mismatchTargetId === null) return null;
  return (
    <Suspense fallback={null}>
      <KpiMismatchModalSlot
        role={isManager ? 'manager' : 'team-leader'}
        kpiTargetId={mismatchTargetId}
        onClose={closeMismatch}
      />
    </Suspense>
  );
}
