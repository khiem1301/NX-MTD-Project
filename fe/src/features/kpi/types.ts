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
};


export type KpiTargetFilter = {
  page: number;
  pageSize: number;
  keyword?: string;
  status?: KpiTargetStatus;
};

/**
 * MI-18 "KPI theo ngày": một dòng kết quả theo ngày của một chỉ tiêu KPI (vw_KpiDailyResult).
 * Có dòng cho mọi ngày có chỉ tiêu (chưa có hàng thì finishedQty = 0) và ngày QA/QC có nhập hàng ngoài kế hoạch
 * (dailyTarget, percentAchieved = null).
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
export type KpiRole = 'manager' | 'admin';

export type SaveKpiTargetPayload = {
  orderId: number;
  productId: number;
  sewingTeamId: number;
  startDate: string;
  endDate: string;
  targetFinishedQty: number;
};

/** Lịch KPI theo ngày. */
export type KpiDailyTargetInput = { workDate: string; targetQty: number };

export type KpiCalendarDay = {
  workDate: string;
  isFuture: boolean;
  isToday: boolean;
  targetQty: number;
  cumTarget: number;
  cutQty: number;
  actualQty: number | null;
  btpRemaining: number;
  lowBtp: boolean;
  behindTarget: boolean;
};

export type KpiCalendarSummary = {
  today: string;
  planTotal: number;
  allocated: number;
  unallocated: number;
  cutTotal: number;
  outTotal: number;
  totalBtp: number;
  committedAll: number;
  committedThis: number;
  remainingToSet: number;
  canEdit: boolean;
};

export type KpiCalendar = {
  kpiTargetId: number;
  orderCode: string;
  productCode: string;
  productName: string;
  sewingTeamName: string;
  startDate: string;
  endDate: string;
  status: KpiTargetStatus;
  summary: KpiCalendarSummary;
  days: KpiCalendarDay[];
};

export type SaveKpiDailyPlanPayload = { dailyTargets: KpiDailyTargetInput[] };
