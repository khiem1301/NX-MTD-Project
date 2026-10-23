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
  /** Chỉ có ở chi tiết: chỉ tiêu từng ngày (null ở danh sách). */
  dailyTargets: KpiDailyTargetInput[] | null;
};

export type KpiDailyTargetInput = { workDate: string; targetQty: number };

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
  /** Bắt buộc: tổng cả kỳ và số ngày làm việc do BE tính từ các ngày này. */
  dailyTargets: KpiDailyTargetInput[];
};
