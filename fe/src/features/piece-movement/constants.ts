import type { StatusLabel } from '@/components/common/StatusTag';

export const MOVEMENT_TYPE = { HANDOVER: 'HANDOVER', RETRIEVAL: 'RETRIEVAL' } as const;

/** Mỗi lần lưu tối đa số dòng này (BE: SaveDailyAllocationDtoValidator.MaxItems). */
export const DAILY_MAX_ITEMS = 500;
export const DAILY_MAX_QTY = 100000;
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
  REJECTED: { text: 'Từ chối / đã rút lại', color: 'red' },
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
  SYSTEM: 'Hệ thống tự xác nhận',
};

export const PM_LABELS = {
  leaderTitle: 'Giao sản lượng công đoạn theo ngày',
  leaderSubtitle:
    'Mỗi ngày lấy sẵn các công đoạn đã phân công cho công nhân, tổ trưởng nhập số lần giao thêm; công nhân xác nhận số được giao (tổ trưởng xác nhận hộ được ở tab Phiếu giao / thu hồi). Phần hôm trước chưa làm hết chuyển sang hôm nay cho chính công nhân đó, không thu hồi. Trần sản lượng cuối ca = tồn từ hôm trước + phần giao đã xác nhận. Thêm được công đoạn ngoài phân công.',
  workerTitle: 'Sản lượng công đoạn được giao',
  boardTab: 'Giao theo ngày',
  historyTab: 'Phiếu giao / thu hồi',
  workDate: 'Ngày làm việc',
  selectOrder: 'Chọn đơn hàng để giao việc',
  selectFirst: 'Chọn đơn hàng trước',
  selectOrderLine: 'Chọn mã hàng và màu - size để giao việc',
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
  unitRate: 'Đơn giá',
  noRate: 'Chưa có đơn giá',
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
  carried: 'Tồn từ hôm trước',
  allocated: 'Giao trong ngày',
  output: 'Đã làm',
  remaining: 'Chưa làm hết',
  unallocated: 'Còn giao được',
  operationCount: 'Số công đoạn',
  giveWork: 'Giao việc',
  giveLikePrevious: 'Giao như lần trước',
  giveLikePreviousHint: 'Điền lại số của lần giao gần nhất cho các công đoạn đã từng giao',
  workerModalTitle: 'Giao việc: {0}',
  workerModalHint:
    'Công đoạn đã phân công được lấy sẵn. Nhập ở dòng nhóm để điền cùng số cho cả nhóm. Số 0 = bỏ phần giao thêm của ngày. Số giao không vượt số còn giao được; công nhân không xác nhận sau 1 giờ thì tự xác nhận đủ.',
  workerHint:
    'Xác nhận hoặc từ chối số được giao. Quá 1 giờ chưa xác nhận thì hệ thống tự xác nhận đủ số.',
  components: 'Thành phần',
  noComponents: 'chưa gắn',
  pendingConfirm: 'Chờ CN xác nhận {0}',
  onBehalfWarnTitle: 'Xác nhận hộ công nhân?',
  onBehalfWarnContent:
    'Số được giao lẽ ra do công nhân tự xác nhận. Chỉ xác nhận hộ khi công nhân không tự xác nhận được (vắng, không có điện thoại…). Thao tác được ghi lại kèm lý do và không hoàn tác được.',
  onBehalfWarnOk: 'Vẫn xác nhận hộ',
  groupRow: 'Nhóm {0} ({1} công đoạn)',
  noGroup: 'chưa đặt tên',
  groupInput: 'Số lần giao nhóm {0}',
  groupPlaceholder: 'Cả nhóm',
  groupChanged: 'Đã sửa {0}',
  groupOver: '{0} vượt số giao được',
  searchOperation: 'Tìm mã, tên công đoạn',
  noMatchOperation: 'Không có công đoạn khớp.',
  operationInput: 'Số lần giao {0}',
  outsideAssignment: 'Ngoài phân công',
  addOperation: 'Thêm công đoạn ngoài phân công',
  addOperationPlaceholder: 'Chọn công đoạn để thêm',
  reasonLabel: 'Lý do hạ số giao (bắt buộc khi giảm)',
  changes: 'Đã sửa {0} dòng',
  noChanges: 'Chưa thay đổi gì.',
  noBoard: 'Đơn này chưa có màu - size nào được chia cho tổ nên chưa có việc để giao.',
  pastDayReadOnly: 'Ngày đã qua: chỉ xem, không giao hay sửa được nữa.',
  noWorkers: 'Không có công nhân phù hợp.',
  lineSizeSeparator: '-',
  // DEV-ALL-126: xác nhận hàng loạt.
  confirmSelected: 'Xác nhận các phiếu đã chọn ({0})',
  selectAllPending: 'Chọn tất cả phiếu đang chờ',
  clearSelection: 'Bỏ chọn',
  batchTitle: 'Xác nhận {0} phiếu giao việc',
  batchHint:
    'Số xác nhận mặc định bằng số giao. Nhận khác thì sửa số và ghi lý do lệch (bắt buộc).',
  sharedReason: 'Lý do chung',
  sharedReasonPlaceholder: 'Điền nhanh cho mọi phiếu lệch chưa có lý do',
  applySharedReason: 'Áp dụng',
  missingReasons: 'Còn {0} phiếu lệch số chưa có lý do.',
  // DEV-ALL-182: phiếu của tổ trưởng gộp theo công nhân.
  slipCount: 'Số phiếu',
  totalHandover: 'Tổng giao',
  totalRetrieval: 'Tổng thu hồi',
  pendingConfirmTitle: 'Chờ CN xác nhận',
  pendingSlips: '{0} phiếu',
  pendingHandover: 'Giao {0}',
  pendingRetrieval: 'Thu hồi {0}',
  confirmWorkerOnBehalf: 'Xác nhận hộ cả công nhân',
  workerOnBehalfHint:
    'Xác nhận đủ số trên mọi phiếu đang chờ của công nhân này, cùng một lý do. Phiếu nhận khác số thì để công nhân tự xác nhận.',
  workerOnBehalfSummary: '{0} phiếu: giao {1} lần, thu hồi {2} lần',
} as const;

export const PM_MESSAGES = {
  confirmed: 'Đã xác nhận.',
  rejected: 'Đã từ chối phiếu.',
  discrepancyReasonRequired: 'Số xác nhận khác số ghi trên phiếu, vui lòng nhập lý do.',
  dailySaved: 'Đã lưu giao việc trong ngày.',
  batchConfirmed: 'Đã xác nhận các phiếu đã chọn.',
} as const;

/** Tối đa phiếu mỗi lần xác nhận hàng loạt (khớp ConfirmWorkAllocationBatchDtoValidator.MaxItems ở BE). */
export const MAX_BATCH_CONFIRM = 100;

export const STATUS_OPTIONS = Object.entries(MOVEMENT_STATUS_LABEL).map(([value, l]) => ({
  value,
  label: l.text,
}));

export const TYPE_OPTIONS = Object.entries(MOVEMENT_TYPE_LABEL).map(([value, label]) => ({
  value,
  label,
}));
