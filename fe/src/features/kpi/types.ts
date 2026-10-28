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

/** Chỉ tiêu tối đa lập được: BTP kho còn + tổ đang giữ − phần còn phải làm của chỉ tiêu khác. */
export type KpiCapacity = {
  warehouseBtp: number;
  teamHeld: number;
  committedByOthers: number;
  maxTarget: number;
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
export type KpiRole = 'manager' | 'admin';

export type SaveKpiTargetPayload = {
  orderId: number;
  productId: number;
  sewingTeamId: number;
  startDate: string;
  endDate: string;
  targetFinishedQty: number;
  workingDays: number;
};
