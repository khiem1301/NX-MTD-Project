/** Đối soát MEALS: hóa đơn bếp đã nhập so với Σ DailyMeal.TotalAmount trong tháng — cảnh báo mềm. */
export type MealsReconciliation = {
  expectedAmount: number;
  importedAmount: number | null;
  hasDiscrepancy: boolean;
};

export type CostFixedItem = { costCategoryCode: string; amount: number };

export type CostVariableItem = {
  costCategoryCode: string;
  productId: number;
  productCode: string;
  productName: string;
  amount: number;
};

export type CostByProduct = { productId: number; productCode: string; productName: string; amount: number };

/**
 * UC12 "Tổng hợp chi phí" (MI-19). CHỈ Admin xem được (GBR-02 — dữ liệu "mật"). Không copy lại chi phí
 * tháng (MonthlyCost, MI-12) ra một kiểu riêng — cộng thêm lương (Payroll đã duyệt) trên cùng một DTO.
 */
export type CostSummary = {
  periodId: number;
  periodMonth: string;
  isLocked: boolean;
  fixedCostTotal: number;
  fixedCosts: CostFixedItem[];
  variableCostTotal: number;
  variableCosts: CostVariableItem[];
  variableCostsByProduct: CostByProduct[];
  monthlyCostTotal: number;
  payrollApproved: boolean;
  payrollTotal: number | null;
  grandTotal: number;
  mealsReconciliation: MealsReconciliation;
};

export type CostSummaryFilter = { periodMonth: string };
