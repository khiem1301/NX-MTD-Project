/** Chuỗi hiển thị màn "Báo cáo kế toán" (UC36, MI-19, CHỈ Kế toán). */
export const ACCOUNTING_REPORT_TEXT = {
  pageTitle: 'Báo cáo kế toán',
  pageSubtitle: 'Công + tăng ca, suất ăn + đối soát bếp, km + xăng theo xe — theo tháng.',
  periodMonth: 'Tháng',
  export: 'Xuất Excel',
  noDataYet: 'Chưa có dữ liệu cho tháng này.',

  tabs: {
    workdays: 'Công + tăng ca',
    meals: 'Suất ăn',
    vehicles: 'Sổ xe',
  },

  workdayTable: {
    employeeCode: 'Mã NV',
    fullName: 'Họ tên',
    teamName: 'Tổ',
    regularWorkdays: 'Công ngày thường',
    sundayWorkdays: 'Công Chủ nhật',
    totalWorkdays: 'Tổng công',
    totalOvertimeHours: 'Giờ tăng ca',
  },

  mealSummary: {
    daysWithData: 'Số ngày có dữ liệu',
    totalMealsServed: 'Tổng suất ăn (hệ thống)',
    totalMealAmountSystem: 'Tổng tiền suất ăn (hệ thống)',
    monthlyCostMealsAmount: 'Hóa đơn bếp đã nhập (chi phí tháng)',
    notImported: 'Chưa nhập',
    discrepancy: 'Số đã nhập khác với tổng suất ăn trong hệ thống, vui lòng kiểm tra lại.',
    match: 'Khớp với hóa đơn bếp.',
  },

  vehicleTable: {
    plateNumber: 'Biển số',
    vehicleType: 'Loại xe',
    completedTripCount: 'SL chuyến hoàn thành',
    totalKm: 'Tổng km',
    totalFuelLiters: 'Tổng xăng (lít)',
    totalFuelCost: 'Tổng tiền xăng',
    kmGapWarningCount: 'SL chuyến cảnh báo km',
    fuelOverNormWarningCount: 'SL chuyến vượt định mức xăng',
  },
} as const;
