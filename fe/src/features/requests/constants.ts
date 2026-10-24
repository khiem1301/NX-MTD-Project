import type { StatusLabel } from '@/components/common/StatusTag';
import { REQUEST_STATUS_LABEL, type RequestStatus, type RequestType } from '@/constants/requestTypes';
import type { AuditEntry } from '@/types/attachment';

const STATUS_COLOR: Record<RequestStatus, string> = {
  PendingApproval: 'gold',
  PendingProcessing: 'blue',
  Approved: 'cyan',
  Rejected: 'red',
  Completed: 'green',
  Cancelled: 'default',
};

export const REQUEST_STATUS_TAG: Record<string, StatusLabel> = Object.fromEntries(
  (Object.keys(REQUEST_STATUS_LABEL) as RequestStatus[]).map((s) => [
    s,
    { text: REQUEST_STATUS_LABEL[s], color: STATUS_COLOR[s] },
  ]),
);

/** Loại đơn dùng form chung của trang này (Cơ điện, Xin NVL / Xin BTP, Bổ sung NVL, NVL lỗi). Loại khác: luồng sở hữu làm form riêng. */
export const GENERIC_FORM_TYPES: readonly RequestType[] = [
  'SpareParts', 'NewMachine', 'MachineSupplies', 'MaterialRequisition', 'SemiFinishedRequisition',
  'MaterialSupplement', 'DefectiveMaterial',
];

/** Dùng khi chưa có chi tiết đơn nào để đọc `maxPhotos` (SystemSetting REQUEST_MAX_PHOTOS). BE vẫn chặn. */
export const DEFAULT_MAX_PHOTOS = 5;

/** D18: sửa / hủy = UPDATE, duyệt = APPROVE, từ chối = REJECT, hoàn thành = CONFIRM. */
export const AUDIT_ACTION_LABEL: Partial<Record<AuditEntry['action'], string>> = {
  UPDATE: 'Cập nhật',
  APPROVE: 'Duyệt',
  REJECT: 'Từ chối',
  CONFIRM: 'Hoàn thành',
};
