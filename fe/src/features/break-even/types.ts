/** UC11 — Hòa vốn + giá trị thặng dư theo mã hàng. CHỈ Admin (dữ liệu "mật" theo GBR-02). */
export type AllocationBasis = 'Output' | 'LabourHours';

export type BreakEvenResultItem = {
  breakEvenResultId: number;
  periodId: number;
  periodMonth: string;
  productId: number;
  productCode: string;
  productName: string;
  allocationBasis: AllocationBasis;
  finishedQty: number;
  cmtUnitPrice: number;
  revenue: number;
  variableCost: number;
  allocatedFixedCost: number;
  breakEvenQty: number | null;
  percentReached: number | null;
  surplusValue: number;
  isAtRisk: boolean;
  calculatedAt: string;
  /** MSG26 (hoặc biến thể khi không tính được điểm hòa vốn), đã dựng sẵn ở BE — chỉ hiện khi isAtRisk. */
  atRiskWarning: string | null;
};

export type BreakEvenCalculationResult = {
  periodId: number;
  periodMonth: string;
  allocationBasis: AllocationBasis;
  calculatedAt: string;
  items: BreakEvenResultItem[];
  atRiskWarnings: string[];
};

export type BreakEvenListResult = {
  periodId: number;
  periodMonth: string;
  items: BreakEvenResultItem[];
};

export type BreakEvenFilter = {
  periodMonth: string;
  productId?: number;
  /** false (mặc định) = chỉ lần tính gần nhất; true = toàn bộ lịch sử các lần tính. */
  history?: boolean;
};
