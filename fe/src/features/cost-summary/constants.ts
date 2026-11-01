/** Chuỗi hiển thị màn "Tổng hợp chi phí" (UC12, MI-19, CHỈ Admin). */
export const COST_SUMMARY_TEXT = {
  pageTitle: 'Tổng hợp chi phí',
  pageSubtitle:
    'Vật tư (nhập ở Chi phí tháng) + lương (bảng lương đã duyệt), tổng và theo mã hàng. Dữ liệu mật — chỉ Admin xem được.',
  periodMonth: 'Tháng',
  export: 'Xuất Excel',
  noDataYet: 'Tháng này chưa có dữ liệu chi phí. Vào màn Chi phí tháng để nhập trước.',

  categoryLabel: {
    Meals: 'Suất ăn',
    Fuel: 'Xăng xe',
    Utilities: 'Điện nước',
    Premises: 'Mặt bằng',
    MachineRepair: 'Sửa chữa, mua sắm máy',
    MachineSupplies: 'Vật tư máy móc',
    Stationery: 'Văn phòng phẩm',
    MaterialsBought: 'NVL mua từ NCC',
    Scrap: 'Phế phẩm',
    Other: 'Khác',
  } as Record<string, string>,

  summary: {
    fixedCostTotal: 'Tổng chi phí cố định',
    variableCostTotal: 'Tổng chi phí biến đổi',
    monthlyCostTotal: 'Tổng chi phí (vật tư)',
    payrollTotal: 'Lương (bảng lương đã duyệt)',
    grandTotal: 'Tổng cộng',
    locked: 'Tháng đã khóa',
  },

  fixedTable: {
    category: 'Loại chi phí',
    amount: 'Số tiền',
  },

  byProductTable: {
    product: 'Mã hàng',
    amount: 'Số tiền',
  },

  reconciliation: {
    title: 'Đối soát suất ăn (MEALS)',
    expected: 'Tổng suất ăn trong hệ thống',
    imported: 'Số đã nhập (hóa đơn bếp)',
    match: 'Khớp với hóa đơn bếp.',
    mismatch: 'Số đã nhập khác với tổng suất ăn trong hệ thống, vui lòng kiểm tra lại.',
  },
} as const;

export const costCategoryLabel = (code: string): string =>
  COST_SUMMARY_TEXT.categoryLabel[code] ?? code;
