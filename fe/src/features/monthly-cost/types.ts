/** Khoản chi phí tháng (MonthlyCost.CostCategoryCode) — UC12, 10 khoản cố định theo schema CHECK. */
export type MonthlyCostCategory =
  | 'Meals'
  | 'Fuel'
  | 'Utilities'
  | 'Premises'
  | 'MachineRepair'
  | 'MachineSupplies'
  | 'Stationery'
  | 'MaterialsBought'
  | 'Scrap'
  | 'Other';

/** Kiểu chi phí (MonthlyCost.CostType) — VARIABLE bắt buộc mã hàng. */
export type MonthlyCostType = 'Fixed' | 'Variable';

/** Trạng thái một dòng trong bảng xem trước Excel (không lưu DB) — MSG24. */
export type MonthlyCostRowStatus = 'Valid' | 'Error' | 'Duplicate';

/** Một dòng trong bảng xem trước: loại chi phí/kiểu/mã hàng lấy nguyên văn từ file (chưa chuẩn hóa). */
export type MonthlyCostExcelRowPreview = {
  rowNumber: number;
  costCategoryCode: string | null;
  costType: string | null;
  productCode: string | null;
  productName: string | null;
  amount: number | null;
  quantity: number | null;
  note: string | null;
  rowStatus: MonthlyCostRowStatus;
  errorMessage: string | null;
};

/** Đối soát MEALS: hóa đơn bếp đã nhập so với Σ DailyMeal.TotalAmount trong tháng — cảnh báo mềm. */
export type MealsReconciliation = {
  expectedAmount: number;
  importedAmount: number | null;
  hasDiscrepancy: boolean;
};

export type MonthlyCostExcelPreview = {
  validCount: number;
  errorCount: number;
  duplicateCount: number;
  rows: MonthlyCostExcelRowPreview[];
  mealsReconciliation: MealsReconciliation;
};

export type MonthlyCostImportResult = {
  periodId: number;
  periodMonth: string;
  validCount: number;
  errorCount: number;
  duplicateCount: number;
  itemCount: number;
  totalAmount: number;
  mealsReconciliation: MealsReconciliation;
};

/** Một dòng chi phí đã lưu (CodeText.ToPascal áp lên CostCategoryCode/CostType khi BE serialize). */
export type MonthlyCostItem = {
  monthlyCostId: number;
  costCategoryCode: MonthlyCostCategory;
  costType: MonthlyCostType;
  productId: number | null;
  productCode: string | null;
  productName: string | null;
  amount: number;
  quantity: number | null;
  note: string | null;
  importedAt: string;
};

export type MonthlyCostSummary = {
  periodId: number;
  periodMonth: string;
  isLocked: boolean;
  totalAmount: number;
  items: MonthlyCostItem[];
  mealsReconciliation: MealsReconciliation;
};
