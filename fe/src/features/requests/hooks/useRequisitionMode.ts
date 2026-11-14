import { useAuth } from '@/hooks/useAuth';
import type { RequestType } from '@/constants/requestTypes';
import type { RequestDetail } from '@/types/request';
import { CUTTING_TEAM_TYPE, SEWING_TEAM_TYPE } from '../constants';

export type RequisitionMode =
  | { kind: 'none' }
  | {
      kind: 'requisition';
      requestType: 'MaterialRequisition' | 'SemiFinishedRequisition';
      /** Đơn xin NVL của tổ cắt: chọn lệnh cắt (không bắt buộc, DEV-ALL-92). */
      byWorkOrder: boolean;
      /** Xin BTP và Xin NVL của tổ may: bắt buộc chọn đơn hàng (DEV-ALL-110). */
      needsOrder: boolean;
    };

/** Cách nhập dòng của form đơn theo loại đơn và loại tổ của người tạo. */
export const useRequisitionMode = (
  requestType: RequestType | undefined,
  editing: RequestDetail | undefined,
): RequisitionMode => {
  const { user } = useAuth();
  if (requestType === 'SemiFinishedRequisition')
    return { kind: 'requisition', requestType, byWorkOrder: false, needsOrder: true };
  if (requestType !== 'MaterialRequisition') return { kind: 'none' };
  const byWorkOrder = editing ? editing.workOrderId !== null : user?.teamType === CUTTING_TEAM_TYPE;
  return {
    kind: 'requisition',
    requestType,
    byWorkOrder,
    needsOrder: !byWorkOrder && user?.teamType === SEWING_TEAM_TYPE,
  };
};
