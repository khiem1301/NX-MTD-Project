import type { PageParams } from '@/types/api';

/** Vai trò gọi API sản lượng / hàng hỏng (tổ trưởng và tổ phó dùng chung đường dẫn team-leader). */
export type OutputRole = 'team-leader' | 'manager';
export type QcRole = 'qa-qc' | 'manager';
export type PieceRateRole = 'accountant' | 'admin';

export type ShiftCode = 'Morning' | 'Afternoon' | 'Overtime';
/** BE trả ca dạng mã DB ("MORNING") ở một số DTO, nhận cả hai dạng khi gửi lên. */
export type ShiftValue = ShiftCode | 'MORNING' | 'AFTERNOON' | 'OVERTIME';

// ---------- KI-01: công đoạn + đơn giá ----------

export type OperationStatus = 'Active' | 'Discontinued';

/** Đơn giá mới nhất của công đoạn nếu chưa thành đơn giá hiện hành (Nháp, Chờ duyệt, Bị từ chối, hoặc đã duyệt nhưng chưa tới ngày hiệu lực). */
export type OperationRate = {
  pieceRateId: number;
  rate: number;
  effectiveDate: string;
  status: PieceRateStatus;
  rejectReason: string | null;
};

export type Operation = {
  operationId: number;
  productId: number;
  productCode: string;
  operationCode: string;
  operationName: string;
  stationSequence: number;
  skillLevel: number | null;
  standardTimeSec: number | null;
  teamType: 'Sewing' | 'Cutting';
  isFinalStep: boolean;
  status: OperationStatus;
  currentRate: number | null;
  nextRate: OperationRate | null;
  /** Đơn giá đã duyệt, chưa tới ngày hiệu lực, gần nhất; cột Đơn giá chỉ hiện bản này. */
  upcomingRate: OperationRate | null;
  operationGroupId: number | null;
  /** Nhóm chứa công đoạn này (vd "May tay"). */
  groupName: string | null;
  /** Các chi tiết (thành phần) công đoạn làm trên đó. */
  componentIds: number[];
};

/** Dòng xem trước khi nhập công đoạn từ Excel. Action: dòng nào thêm mới / cập nhật / không đổi / lỗi. */
export type OperationImportAction = 'New' | 'Update' | 'Unchanged' | 'Error';
export type OperationImportRateAction = 'None' | 'New' | 'Update' | 'Unchanged' | 'Skipped';

export type OperationImportRow = {
  rowNumber: number;
  stt: number;
  operationCode: string;
  operationName: string | null;
  skillLevel: number | null;
  standardTimeSec: number | null;
  equipment: string | null;
  rate: number | null;
  action: OperationImportAction;
  error: string | null;
  /** Đơn giá từ cột TIỀN: None (không xử lý) | New | Update | Unchanged | Skipped. */
  rateAction: OperationImportRateAction;
  rateNote: string | null;
};

export type OperationImportPreview = {
  sheetName: string;
  sheetNames: string[];
  productText: string | null;
  newCount: number;
  updateCount: number;
  unchangedCount: number;
  errorCount: number;
  productHasFinalStep: boolean;
  hasRateColumn: boolean;
  rateNewCount: number;
  rateUpdateCount: number;
  rateUnchangedCount: number;
  rateSkippedCount: number;
  rows: OperationImportRow[];
};

export type OperationImportResult = {
  created: number;
  updated: number;
  unchanged: number;
  skipped: number;
  rateCreated: number;
  rateUpdated: number;
  rateUnchanged: number;
  rateSkipped: number;
};

export type OperationImportParams = {
  productId: number;
  sheetName?: string;
  finalStt?: number;
  /** Có ngày này thì đọc luôn cột TIỀN thành đơn giá công đoạn hiệu lực từ ngày đó (yyyy-MM-dd). */
  effectiveDate?: string;
  /** Gửi Admin duyệt luôn (Chờ duyệt) thay vì để Nháp. */
  submitRates?: boolean;
};

/** API công đoạn theo vai trò: Admin không đặt được đơn giá (DEV-ALL-50). */
export type OperationRole = 'accountant' | 'admin';

/** Nhóm công đoạn của mã hàng; `operationCount` tính cả công đoạn đã ngừng dùng. */
export type OperationGroup = {
  operationGroupId: number;
  groupName: string;
  operationCount: number;
};

export type SetOperationsGroupPayload = {
  productId: number;
  operationIds: number[];
  operationGroupId: number | null;
};

export type SaveOperationPayload = {
  productId: number;
  operationCode: string;
  operationName: string;
  stationSequence: number;
  skillLevel?: number | null;
  standardTimeSec?: number | null;
  teamType?: 'Sewing' | 'Cutting';
  isFinalStep: boolean;
  groupName?: string | null;
  componentIds?: number[];
};

export type PieceRateStatus = 'Draft' | 'PendingApproval' | 'Approved' | 'Rejected';

export type PieceRate = {
  pieceRateId: number;
  operationId: number;
  operationCode: string;
  operationName: string;
  productId: number;
  productCode: string;
  rate: number;
  effectiveDate: string;
  status: PieceRateStatus;
  submittedAt: string | null;
  approvedAt: string | null;
  approvalNote: string | null;
  rejectReason: string | null;
};

export type PieceRateQuery = Pick<PageParams, 'page' | 'pageSize'> & {
  operationId?: number;
  productId?: number;
  status?: PieceRateStatus;
};

export type SavePieceRatePayload = { operationId: number; rate: number; effectiveDate: string };

// ---------- KI-03: phân công ----------

export type OperationAssignment = {
  operationAssignmentId: number;
  orderId: number;
  operationId: number;
  operationCode: string;
  operationName: string;
  productId: number;
  productCode: string;
  workerId: number;
  workerCode: string;
  workerName: string;
  validFrom: string;
};

export type OperationOption = {
  operationId: number;
  productId: number;
  productCode: string;
  operationCode: string;
  operationName: string;
  stationSequence: number;
  isFinalStep: boolean;
  groupName: string | null;
  skillLevel: number | null;
  /** Thời gian chuẩn của công đoạn (giây). */
  standardTimeSec?: number | null;
};

/** Một dòng đơn (mã hàng × màu × size) của đơn đang mở và các tổ may được chia (DEV-ALL-49). */
export type OrderLineTeamRow = {
  orderLineId: number;
  orderId: number;
  orderCode: string;
  dueDate: string;
  productId: number;
  productCode: string;
  productName: string;
  color: string;
  sizeCode: string;
  orderedQty: number;
  teams: { teamId: number; teamName: string }[];
};

export type OrderLineTeamParams = {
  keyword?: string;
  onlyUnallotted?: boolean;
  page: number;
  pageSize: number;
};

export type SetOrderLineTeamsPayload = { orderLineId: number; teamIds: number[] };

export type AddOrderLineTeamsPayload = { orderLineIds: number[]; teamIds: number[] };

export type AssignPayload = { orderId: number; operationId: number; workerId: number };
export type AssignManyPayload = { orderId: number; workerId: number; operationIds: number[] };
export type CopyAssignmentsPayload = { fromOrderId: number; toOrderId: number };

// ---------- KI-04: sản lượng cuối ca ----------

export type OutputGridRow = {
  operationId: number;
  operationCode: string;
  operationName: string;
  /** Nhóm công đoạn; nhập sản lượng nhóm sẽ điền cho mọi công đoạn trong nhóm. */
  groupName?: string | null;
  workerId: number;
  workerCode: string;
  workerName: string;
  outputRecordId: number | null;
  quantity: number | null;
  unitRate: number | null;
  rateApproved: boolean;
  isOutsideAssignment: boolean;
  noAttendanceWarning: boolean;
  canEdit: boolean;
  editDeadline: string | null;
  rowVer: string | null;
  /** Số lần công nhân đã được giao ở công đoạn này (trần sản lượng). */
  capQty: number;
  /** Tổng sản lượng đã báo của công nhân ở công đoạn này (mọi ca, mọi ngày). */
  reportedQty: number;
  /** Báo vượt số được giao thì chuyển được tối đa chừng này từ người cùng công đoạn còn dư và phần tổ chưa giao. */
  transferableQty: number;
};

export type OutputGrid = {
  workDate: string;
  shiftCode: ShiftValue;
  orderLineId: number;
  sewingTeamId: number;
  receivedFromWarehouse: number;
  rows: OutputGridRow[];
};

export type OutputGridQuery = {
  workDate: string;
  shiftCode: ShiftCode;
  orderLineId: number;
  sewingTeamId?: number;
};

export type OutputLineInput = {
  operationId: number;
  workerId: number;
  quantity: number;
  rowVer?: string | null;
};

export type SaveOutputPayload = {
  workDate: string;
  shiftCode: ShiftCode;
  orderLineId: number;
  sewingTeamId?: number;
  reason?: string;
  rows: OutputLineInput[];
  /** true = dòng vượt số được giao thì chuyển phần vượt trước khi lưu (tổ trưởng đã xác nhận). */
  transferExcess?: boolean;
};

export type OutputSaveResult = {
  inserted: number;
  updated: number;
  deleted: number;
  warnings: string[];
};

// ---------- KI-06: QC ----------

export type QcBatch = {
  qcBatchId: number;
  workDate: string;
  shiftCode: ShiftValue;
  batchTime: string;
  sewingTeamId: number;
  sewingTeamName: string;
  orderLineId: number;
  orderCode: string;
  productCode: string;
  color: string;
  sizeCode: string;
  finishedQty: number;
  inspectorId: number;
  inspectorName: string;
  editDeadline: string;
  canEdit: boolean;
  rowVer: string;
};

export type QcBatchQuery = Pick<PageParams, 'page' | 'pageSize'> & {
  workDate?: string;
  sewingTeamId?: number;
  orderLineId?: number;
};

export type CreateQcBatchPayload = {
  workDate: string;
  shiftCode: ShiftCode;
  batchTime: string;
  sewingTeamId: number;
  orderLineId: number;
  finishedQty: number;
};

export type UpdateQcBatchPayload = { finishedQty: number; reason?: string; rowVer: string };
export type DeleteQcBatchPayload = { reason?: string; rowVer: string };

export type OnLineQty = {
  sewingTeamId: number;
  orderLineId: number;
  received: number;
  finished: number;
  scrap: number;
  onLine: number;
};

// ---------- KI-08: hàng hỏng ----------

export type DefectType = 'Rework' | 'Scrap';
export type DefectStatus = 'Recorded' | 'Confirmed' | 'Cancelled';
export type DefectReason =
  'SkippedStitch' | 'CrookedSeam' | 'WrongSize' | 'Torn' | 'Stained' | 'Other';

export type DefectObjectionFilter = 'Pending' | 'Resolved' | 'None';

export type Defect = {
  defectRecordId: number;
  defectCode: string;
  workDate: string;
  shiftCode: ShiftValue | null;
  sewingTeamId: number;
  orderLineId: number;
  orderCode: string;
  productCode: string;
  operationId: number;
  operationCode: string;
  operationName: string;
  responsibleWorkerId: number;
  responsibleWorkerName: string;
  defectType: DefectType;
  quantity: number;
  defectReasonCode: DefectReason;
  reasonNote: string | null;
  scrapUnitRate: number | null;
  workerNotifiedAt: string | null;
  workerObjection: string | null;
  objectedAt: string | null;
  editDeadline: string;
  status: DefectStatus;
  recordedAt: string;
  canEdit: boolean;
  rowVer: string;
  orderId: number;
  /** Đơn cắt bù của phiếu (mỗi phiếu tối đa một đơn). */
  replacementRequestId: number | null;
  replacementRequestCode: string | null;
  replacementRequestStatus: string | null;
  sewingTeamName: string;
  /** Lần cập nhật gần nhất; sau khi chấp nhận hoặc hủy phiếu chính là giờ của thao tác đó. */
  updatedAt: string | null;
};

export type DefectQuery = Pick<PageParams, 'page' | 'pageSize'> & {
  workDate?: string;
  from?: string;
  to?: string;
  /** Tìm theo tên công nhân, mã hàng, tổ, mã phiếu, mã đơn. */
  keyword?: string;
  /** Chỉ Quản đốc lọc theo tổ; tổ trưởng luôn chỉ thấy tổ mình. */
  sewingTeamId?: number;
  status?: DefectStatus;
  defectType?: DefectType;
  objection?: DefectObjectionFilter;
};

/** Bộ lọc danh sách phiếu hàng hỏng (không gồm số trang). */
export type DefectFilters = Omit<DefectQuery, 'page' | 'pageSize'>;

export type CreateDefectPayload = {
  workDate: string;
  shiftCode?: ShiftCode;
  orderLineId: number;
  operationId: number;
  responsibleWorkerId: number;
  defectType: DefectType;
  quantity: number;
  reasonCode: DefectReason;
  reasonNote?: string;
};

export type CancelDefectPayload = { reason?: string; rowVer: string };
export type ObjectDefectPayload = { objection: string; rowVer: string };
export type ConfirmDefectPayload = { note?: string; rowVer: string };
