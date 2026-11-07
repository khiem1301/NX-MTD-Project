import type { StatusLabel } from '@/components/common/StatusTag';
import {
  REQUEST_STATUS_LABEL,
  type RequestStatus,
  type RequestType,
} from '@/constants/requestTypes';
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

/** Loại đơn dùng form chung của trang này (Cơ điện + đơn Xin NVL / Xin BTP của tổ). Loại khác: luồng sở hữu làm form riêng. */
export const GENERIC_FORM_TYPES: readonly RequestType[] = [
  'SpareParts',
  'NewMachine',
  'MachineSupplies',
  'MaterialRequisition',
  'SemiFinishedRequisition',
  'DefectiveMaterial',
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

/** Giới hạn dòng đơn xin, khớp RequisitionLimits và RequestLineInputValidator ở BE (DEV-ALL-77). */
export const REQUEST_LINE_LIMITS = {
  maxLines: 50,
  /** NVL, linh kiện: tối đa 3 số lẻ. */
  maxMaterialQty: 9_999_999,
  /** Cơ điện xin linh kiện, vật tư máy: số nguyên, tối đa 9.999 (khớp MachineRequestLimits ở BE). */
  maxPartsQty: 9_999,
  /** Cơ điện xin máy mới: số nguyên, tối đa 99. */
  maxMachineQty: 99,
  /** BTP đếm theo bộ (số nguyên). */
  maxPieceQty: 1_000_000,
} as const;

/** Vải được nhập số lẻ tối đa 2 chữ số; NVL khác vải chỉ nhập số nguyên (khớp RequisitionLimits.FabricDecimals ở BE). */
export const FABRIC_MATERIAL_TYPE = 'FABRIC';
export const FABRIC_QTY_DECIMALS = 2;
