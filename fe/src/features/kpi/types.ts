/**
 * Trạng thái / kết quả chỉ tiêu KPI tổ may (KpiTarget.Status / .Result). DB lưu UPPER_SNAKE,
 * `KpiService` tự chuyển sang PascalCase trước khi trả API (Coding Rules 2.2) — DEV-MI-18-2 đã
 * ghi nhận và sửa lại chỗ thiếu này ở BE, FE theo đúng PascalCase như các trạng thái khác.
 */
export type KpiTargetStatus = 'Active' | 'Closed' | 'Cancelled';
export type KpiTargetResult = 'Achieved' | 'NotAchieved';

/** Chỉ tiêu KPI tổ may (UC19) — dùng chung cho danh sách và chi tiết. */
export type KpiTarget = {
  kpiTargetId: number;
  orderId: number;
  orderCode: string;
  productId: number;
  productCode: string;
  productName: string;
  sewingTeamId: number;
  sewingTeamName: string;
  startDate: string;
  endDate: string;
  targetFinishedQty: number;
  workingDays: number;
  dailyTarget: number;
  status: KpiTargetStatus;
  result: KpiTargetResult | null;
  /** MI-17: Σ hàng hoàn thiện cả kỳ (QC đạt) và % so với chỉ tiêu cả kỳ. */
  actualFinishedQty: number;
  percentAchieved: number;
  createdAt: string;
  /** Chỉ tiêu theo màu × size; rỗng ở chỉ tiêu cũ chỉ có số tổng. */
  lines: KpiTargetLine[] | null;
};

/** Chỉ tiêu của một dòng đơn (màu × size); actualQty = QC đạt của tổ ở dòng đó trong kỳ. */
export type KpiTargetLine = {
  orderLineId: number;
  color: string;
  sizeCode: string;
  targetQty: number;
  actualQty: number;
};

/** Một màu × size của mã hàng đã chia cho tổ: số đặt − phần đã giao cho tổ khác = tối đa đặt được. */
export type KpiLineCapacity = {
  orderLineId: number;
  color: string;
  sizeCode: string;
  orderedQty: number;
  assignedToOthers: number;
  maxTarget: number;
  /** QC đạt của chỉ tiêu đang sửa ở dòng này (không hạ chỉ tiêu dưới số này); lập mới thì 0. */
  actualQty: number;
};

/** Chỉ tiêu tối đa lập được cho đơn + mã hàng: số lượng đơn đặt − phần đã giao cho các tổ khác. */
export type KpiOrderCapacity = {
  orderQty: number;
  assignedToOthers: number;
  maxTarget: number;
  dueDate: string | null;
  /** Kỳ kết thúc cách ngày giao không quá số ngày này thì cảnh báo. */
  dueWarningDays: number;
  orderStatus: string | null;
  /** Đơn đã dừng, đã hủy hoặc đã giao xong: không đặt chỉ tiêu được. */
  orderClosed: boolean;
};

/** Một ô trên lịch KPI: targetQty = 0 là chưa đặt, actualQty = null ở ngày chưa tới. */
export type KpiCalendarDay = {
  workDate: string;
  targetQty: number;
  actualQty: number | null;
};

/** BTP khả dụng cho KPI theo ngày: kho + tổ đang giữ − phần các tổ khác phải lấy từ kho. */
export type KpiBtp = {
  warehouseBtp: number;
  teamHeld: number;
  neededByOthers: number;
  available: number;
};

export type KpiCalendar = {
  kpiTargetId: number;
  orderCode: string;
  productCode: string;
  productName: string;
  sewingTeamName: string;
  startDate: string;
  endDate: string;
  today: string;
  dueDate: string;
  /** Ngày sớm nhất còn sửa được KPI: hôm nay nếu chưa tới editCutoff, không thì ngày mai. */
  firstEditableDate: string;
  editCutoff: string;
  targetFinishedQty: number;
  finishedQty: number;
  canEdit: boolean;
  btp: KpiBtp;
  days: KpiCalendarDay[];
};

/** Công nhân đã báo sản lượng một công đoạn ở một dòng đơn lệch, kèm sản lượng đã báo. */
export type EndOfOrderWorker = {
  workerId: number;
  workerCode: string;
  workerName: string;
  reportedQty: number;
};

/** Dòng đơn lệch ở một công đoạn: báo nhiều hơn QC đạt + hàng hủy. Chỉ cảnh báo, không tự trừ lương. Workers: sản lượng giảm dần. */
export type EndOfOrderLine = {
  orderLineId: number;
  color: string;
  sizeCode: string;
  reportedQty: number;
  qcQty: number;
  scrapQty: number;
  mismatchQty: number;
  workers: EndOfOrderWorker[];
};

/** Công đoạn có lệch (chỉ công đoạn lệch được trả về), lệch nhiều nhất lên đầu. */
export type EndOfOrderOperation = {
  operationId: number;
  operationCode: string;
  operationName: string;
  mismatchQty: number;
  lines: EndOfOrderLine[];
};

export type EndOfOrderPreview = {
  kpiTargetId: number;
  orderCode: string;
  productCode: string;
  teamName: string;
  targetQty: number;
  actualQty: number;
  achieved: boolean;
  shortfallQty: number;
  shortfallPct: number;
  thresholdPct: number;
  /** None | AutoApproved | LeaderDecides */
  otDecision: 'None' | 'AutoApproved' | 'LeaderDecides';
  operations: EndOfOrderOperation[];
  blockers: string[];
  canFinish: boolean;
};

export type KpiTargetFilter = {
  page: number;
  pageSize: number;
  keyword?: string;
  status?: KpiTargetStatus;
};

/**
 * MI-18 "KPI theo ngày": một dòng kết quả theo ngày của một chỉ tiêu KPI (vw_KpiDailyResult).
 * Chỉ có dòng cho ngày QA/QC có nhập hàng hoàn thiện — ngày không có dòng là ngày chưa có sản
 * lượng hoàn thiện, không phải lỗi dữ liệu.
 */
export type KpiDailyResult = {
  kpiTargetId: number;
  sewingTeamId: number;
  orderId: number;
  productId: number;
  workDate: string;
  dailyTarget: number | null;
  finishedQty: number | null;
  defectQty: number;
  defectRatePct: number | null;
  percentAchieved: number | null;
};

/** Quản đốc lập / sửa chỉ tiêu KPI; Admin chỉ xem (MI-17, UC21). */

export type SaveKpiTargetPayload = {
  orderId: number;
  productId: number;
  sewingTeamId: number;
  startDate: string;
  endDate: string;
  /** Chỉ tiêu cả kỳ = tổng các dòng, BE tự tính. */
  lines: { orderLineId: number; targetQty: number }[];
};

export type SaveKpiDailyPlanPayload = {
  dailyTargets: { workDate: string; targetQty: number }[];
};
