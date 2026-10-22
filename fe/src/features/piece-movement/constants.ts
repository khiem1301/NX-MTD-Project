import type { StatusLabel } from '@/components/common/StatusTag';

export const MOVEMENT_TYPE = { HANDOVER: 'HANDOVER', RETRIEVAL: 'RETRIEVAL' } as const;

export const MOVEMENT_TYPE_LABEL: Record<string, string> = {
  HANDOVER: 'Giao BTP',
  RETRIEVAL: 'Thu hồi BTP',
};

export const MOVEMENT_STATUS = {
  PENDING: 'PENDING',
  CONFIRMED: 'CONFIRMED',
  REJECTED: 'REJECTED',
} as const;

export const MOVEMENT_STATUS_LABEL: Record<string, StatusLabel> = {
  PENDING: { text: 'Chờ công nhân xác nhận', color: 'gold' },
  CONFIRMED: { text: 'Đã xác nhận', color: 'green' },
  REJECTED: { text: 'Công nhân từ chối', color: 'red' },
};

export const RETRIEVAL_REASON_LABEL: Record<string, string> = {
  OPERATION_CHANGED: 'Đổi công đoạn',
  WORKER_ABSENT: 'Công nhân nghỉ',
  STYLE_FINISHED: 'Hết mã hàng',
};

export const CONFIRM_MODE_LABEL: Record<string, string> = {
  WORKER: 'Công nhân tự xác nhận',
  ON_BEHALF: 'Tổ trưởng xác nhận hộ',
};

export const PM_LABELS = {
  leaderTitle: 'Giao / thu hồi BTP với công nhân',
  workerTitle: 'BTP của tôi',
  create: 'Ghi giao / thu hồi',
  type: 'Loại',
  worker: 'Công nhân',
  selectWorker: 'Chọn công nhân',
  orderLine: 'Dòng đơn',
  selectLine: 'Chọn dòng đơn (màu - size)',
  quantity: 'Số lượng',
  confirmedQty: 'Số xác nhận',
  held: 'Đang giữ',
  available: 'Còn giao được',
  retrievable: 'Còn thu hồi được',
  retrievalReason: 'Lý do thu hồi',
  selectReason: 'Chọn lý do thu hồi',
  status: 'Trạng thái',
  createdAt: 'Ghi lúc',
  confirmedAt: 'Xác nhận lúc',
  confirmMode: 'Cách xác nhận',
  discrepancyReason: 'Lý do lệch',
  onBehalfReason: 'Lý do xác nhận hộ',
  confirmOnBehalf: 'Xác nhận hộ',
  confirm: 'Xác nhận',
  reject: 'Từ chối',
  rejectTitle: 'Từ chối phiếu BTP',
  rejectReason: 'Lý do từ chối',
  order: 'Đơn hàng',
  noHolding: 'Chưa có BTP để giao / thu hồi.',
} as const;

export const PM_MESSAGES = {
  created: 'Đã ghi phiếu, chờ công nhân xác nhận.',
  confirmed: 'Đã xác nhận BTP.',
  rejected: 'Đã từ chối phiếu BTP.',
  discrepancyReasonRequired: 'Số xác nhận khác số ghi trên phiếu, vui lòng nhập lý do.',
} as const;
