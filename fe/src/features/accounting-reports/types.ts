/** UC36 "Báo cáo kế toán" (MI-19). CHỈ Kế toán. Dữ liệu vận hành (giờ công, suất ăn, sổ xe) — KHÔNG chứa
 * đơn giá CMT/chi phí/lương (GBR-02, xem feature cost-summary cho màn "mật" riêng của Admin). */

export type WorkdayOvertimeRow = {
  employeeId: number;
  employeeCode: string;
  fullName: string;
  teamName: string | null;
  regularWorkdays: number;
  sundayWorkdays: number;
  totalWorkdays: number;
  totalOvertimeHours: number;
};

export type WorkdayOvertimeReport = {
  periodMonth: string;
  rows: WorkdayOvertimeRow[];
  totalRegularWorkdays: number;
  totalSundayWorkdays: number;
  totalWorkdays: number;
  totalOvertimeHours: number;
};

export type MealReport = {
  periodMonth: string;
  daysWithData: number;
  totalMealsServed: number;
  totalMealAmountSystem: number;
  monthlyCostMealsAmount: number | null;
  hasDiscrepancy: boolean;
};

export type VehicleTripReportRow = {
  vehicleId: number;
  plateNumber: string;
  vehicleType: string;
  completedTripCount: number;
  totalKm: number;
  totalFuelLiters: number;
  totalFuelCost: number;
  kmGapWarningCount: number;
  fuelOverNormWarningCount: number;
};

export type VehicleReport = {
  periodMonth: string;
  vehicles: VehicleTripReportRow[];
  totalKm: number;
  totalFuelLiters: number;
  totalFuelCost: number;
  totalKmGapWarnings: number;
  totalFuelOverNormWarnings: number;
};

export type AccountingReportFilter = { periodMonth: string };
