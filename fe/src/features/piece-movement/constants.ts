import type { StatusLabel } from '@/components/common/StatusTag';

export const MOVEMENT_TYPE = { HANDOVER: 'HANDOVER', RETRIEVAL: 'RETRIEVAL' } as const;

/** Mỗi lần lưu tối đa số dòng này (BE: SaveDailyAllocationDtoValidator.MaxItems). */
export const DAILY_MAX_ITEMS = 500;
export const DAILY_MAX_QTY = 100000;
export const DAILY_REASON_MAX_LENGTH = 500;
export const DAILY_SEARCH_MAX_LENGTH = 100;

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
  END_OF_DAY: 'Cuối ngày chưa làm hết',
  ADJUSTED: 'Hạ số giao',
};

export const CONFIRM_MODE_LABEL: Record<string, string> = {
  WORKER: 'Công nhân tự xác nhận',
  ON_BEHALF: 'Tổ trưởng xác nhận hộ',
  LEADER: 'Tổ trưởng giao trực tiếp',
  SYSTEM: 'Hệ thống tự thu hồi',
};

export const PM_LABELS = {
  leaderTitle: 'Giao sản lượng công đoạn theo ngày',
  leaderSubtitle:
    'Mỗi ngày lấy sẵn các công đoạn đã phân công cho công nhân, tổ trưởng nhập số lần giao; số này là trần sản lượng cuối ca của ngày. Thêm được công đoạn ngoài phân công. Cuối ngày phần chưa làm hết được thu hồi kèm lý do.',
  workerTitle: 'Sản lượng công đoạn được giao',
  boardTab: 'Giao theo ngày',
  historyTab: 'Phiếu giao / thu hồi',
  workDate: 'Ngày làm việc',
  selectOrder: 'Chọn đơn hàng để giao việc',
  selectFirst: 'Chọn đơn hàng trước',
  allOrderLines: 'Tất cả dòng đơn',
  searchWorker: 'Tìm mã, tên công nhân',
  stateLabel: 'Tình trạng việc',
  stateAll: 'Tất cả ({0})',
  stateIdle: 'Chưa được giao ({0})',
  stateBusy: 'Đã được giao ({0})',
  operation: 'Công đoạn',
  group: 'Nhóm công đoạn',
  worker: 'Công nhân',
  orderLine: 'Dòng đơn',
  type: 'Loại',
  quantity: 'Số lần làm',
  confirmedQty: 'Số lần xác nhận',
  status: 'Trạng thái',
  createdAt: 'Ghi lúc',
  workDateShort: 'Ngày làm việc',
  retrievalNote: 'Lý do thu hồi',
  discrepancyReason: 'Lý do lệch',
  onBehalfReason: 'Lý do xác nhận hộ',
  confirmOnBehalf: 'Xác nhận hộ',
  confirm: 'Xác nhận',
  reject: 'Từ chối',
  rejectTitle: 'Từ chối phiếu giao việc',
  rejectReason: 'Lý do từ chối',
  order: 'Đơn hàng',
  allocated: 'Đã giao trong ngày',
  output: 'Đã làm',
  remaining: 'Chưa làm hết',
  unallocated: 'Còn giao được',
  operationCount: 'Số công đoạn',
  giveWork: 'Giao việc',
  retrieveRemaining: 'Thu hồi cuối ngày',
  retrieveTitle: 'Thu hồi phần chưa làm hết trong ngày',
  retrieveHint:
    'Thu hồi phần được giao nhưng công nhân chưa làm hết của ngày {0}. Nếu không thu hồi, hệ thống tự thu sau khi hết hạn sửa sản lượng của ngày đó.',
  retrieveWorkers: 'Công nhân (để trống là cả tổ)',
  retrieveReason: 'Lý do thu hồi (bắt buộc)',
  retrieveReasonPlaceholder: 'Ví dụ: hết giờ làm, hàng chờ phụ liệu',
  workerModalTitle: 'Giao việc: {0}',
  workerModalHint:
    'Các công đoạn đã phân công cho công nhân được lấy sẵn. Nhập ở dòng nhóm để điền cùng số cho cả nhóm rồi sửa lại từng công đoạn nếu cần. Số 0 là bỏ phần giao của ngày. Số giao không vượt số còn giao được của công đoạn.',
  groupRow: 'Nhóm {0} ({1} công đoạn)',
  noGroup: 'chưa đặt tên',
  groupInput: 'Số lần giao nhóm {0}',
  groupPlaceholder: 'Cả nhóm',
  operationInput: 'Số lần giao {0}',
  outsideAssignment: 'Ngoài phân công',
  addOperation: 'Thêm công đoạn ngoài phân công',
  addOperationPlaceholder: 'Chọn công đoạn để thêm',
  reasonLabel: 'Lý do hạ số giao (bắt buộc khi giảm)',
  changes: 'Đã sửa {0} dòng',
  noChanges: 'Chưa thay đổi gì.',
  noBoard: 'Tổ chưa nhận BTP của đơn này nên chưa có việc để giao.',
  noWorkers: 'Không có công nhân phù hợp.',
  lineSizeSeparator: '-',
} as const;

export const PM_MESSAGES = {
  confirmed: 'Đã xác nhận.',
  rejected: 'Đã từ chối phiếu.',
  discrepancyReasonRequired: 'Số xác nhận khác số ghi trên phiếu, vui lòng nhập lý do.',
  dailySaved: 'Đã lưu giao việc trong ngày.',
  dailyRetrieved: 'Đã thu hồi phần chưa làm hết.',
} as const;

export const STATUS_OPTIONS = Object.entries(MOVEMENT_STATUS_LABEL).map(([value, l]) => ({
  value,
  label: l.text,
}));

export const TYPE_OPTIONS = Object.entries(MOVEMENT_TYPE_LABEL).map(([value, label]) => ({
  value,
  label,
}));
