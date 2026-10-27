import type { StatusLabel } from '@/components/common/StatusTag';

export const MOVEMENT_TYPE = { HANDOVER: 'HANDOVER', RETRIEVAL: 'RETRIEVAL' } as const;

export const MOVEMENT_TYPE_LABEL: Record<string, string> = {
  HANDOVER: 'Giao việc',
  RETRIEVAL: 'Thu hồi việc',
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
  leaderTitle: 'Giao việc cho công nhân',
  workerTitle: 'Việc được giao cho tôi',
  boardTab: 'Chia việc theo công đoạn',
  historyTab: 'Phiếu giao / thu hồi',
  selectOrder: 'Chọn đơn hàng để chia việc',
  searchOperation: 'Tìm công đoạn hoặc công đoạn to',
  teamPieces: 'BTP tổ đang giữ',
  teamPiecesHint: 'Chỉ bộ đủ. BTP chỉ bị trừ khi giao kèm cho công nhân (công đoạn dùng BTP).',
  operation: 'Công đoạn',
  group: 'Công đoạn to',
  required: 'Cần làm',
  allocated: 'Đã giao',
  pending: 'Chờ xác nhận',
  unallocated: 'Chưa giao',
  output: 'Đã làm',
  giveAction: 'Giao',
  retrieveAction: 'Thu hồi',
  workersHolding: 'Công nhân đang được giao',
  giveTitle: 'Giao việc',
  retrieveTitle: 'Thu hồi việc',
  withPieces: 'Kèm BTP',
  withoutPieces: 'Không kèm BTP',
  type: 'Loại',
  worker: 'Công nhân',
  selectWorker: 'Chọn công nhân',
  orderLine: 'Dòng đơn',
  quantity: 'Số lần làm',
  pieceQty: 'BTP giao kèm',
  pieceQtyBack: 'BTP trả lại',
  confirmedQty: 'Số lần xác nhận',
  confirmedPieceQty: 'BTP xác nhận',
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
  rejectTitle: 'Từ chối phiếu giao việc',
  rejectReason: 'Lý do từ chối',
  order: 'Đơn hàng',
  noBoard: 'Tổ chưa nhận BTP của đơn này nên chưa có việc để chia.',
  noWorkers: 'Chưa có công nhân nào đang được giao việc ở công đoạn này.',
} as const;

export const PM_MESSAGES = {
  created: 'Đã ghi phiếu, chờ công nhân xác nhận.',
  confirmed: 'Đã xác nhận.',
  rejected: 'Đã từ chối phiếu.',
  discrepancyReasonRequired: 'Số xác nhận khác số ghi trên phiếu, vui lòng nhập lý do.',
} as const;
