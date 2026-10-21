import type { StatusLabel } from '@/components/common/StatusTag';
import type { MonthlyCostCategory, MonthlyCostRowStatus, MonthlyCostType } from './types';

/** Chuỗi hiển thị của luồng chi phí tháng (MI-12). Thông báo kết quả thao tác nằm ở constants/messages.ts. */
export const MONTHLY_COST_TEXT = {
  pageTitle: 'Chi phí tháng',
  pageSubtitle:
    'Tải file mẫu, điền chi phí tháng (10 khoản cố định/biến đổi) rồi nhập lại; import lại cùng tháng thay số cũ.',
  periodMonth: 'Tháng',
  downloadTemplate: 'Tải file mẫu',
  chooseFile: 'Chọn file Excel',
  preview: 'Xem trước',
  import: 'Nhập chi phí tháng',
  noDataYet: 'Tháng này chưa có dữ liệu chi phí. Hãy tải file mẫu và nhập Excel.',

  previewSummary: {
    valid: 'Hợp lệ',
    error: 'Lỗi',
    duplicate: 'Trùng',
  },

  previewColumns: {
    rowNumber: 'Dòng',
    costCategoryCode: 'Loại chi phí',
    costType: 'Kiểu',
    productCode: 'Mã hàng',
    productName: 'Tên hàng',
    amount: 'Số tiền',
    quantity: 'Số lượng',
    note: 'Ghi chú',
    status: 'Trạng thái',
    errorMessage: 'Ghi chú lỗi',
  },

  rowStatusLabel: {
    Valid: 'Hợp lệ',
    Error: 'Lỗi',
    Duplicate: 'Trùng',
  },

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
  },

  typeLabel: {
    Fixed: 'Cố định',
    Variable: 'Biến đổi',
  },

  summary: {
    periodMonth: 'Tháng',
    totalAmount: 'Tổng chi phí',
    itemCount: 'Số khoản',
    locked: 'Tháng đã khóa',
  },

  table: {
    category: 'Loại chi phí',
    type: 'Kiểu',
    product: 'Mã hàng',
    amount: 'Số tiền',
    quantity: 'Số lượng',
    note: 'Ghi chú',
    importedAt: 'Nhập lúc',
  },

  reconciliation: {
    title: 'Đối soát suất ăn (MEALS)',
    expected: 'Tổng suất ăn trong hệ thống',
    imported: 'Số đã nhập (hóa đơn bếp)',
    match: 'Khớp với hóa đơn bếp.',
    mismatch: 'Số đã nhập khác với tổng suất ăn trong hệ thống, vui lòng kiểm tra lại.',
  },
} as const;

export const MONTHLY_COST_ROW_STATUS_LABEL: Record<MonthlyCostRowStatus, StatusLabel> = {
  Valid: { text: MONTHLY_COST_TEXT.rowStatusLabel.Valid, color: 'green' },
  Error: { text: MONTHLY_COST_TEXT.rowStatusLabel.Error, color: 'red' },
  Duplicate: { text: MONTHLY_COST_TEXT.rowStatusLabel.Duplicate, color: 'gold' },
};

export const MONTHLY_COST_CATEGORY_LABEL: Record<MonthlyCostCategory, string> = MONTHLY_COST_TEXT.categoryLabel;

export const MONTHLY_COST_TYPE_LABEL: Record<MonthlyCostType, StatusLabel> = {
  Fixed: { text: MONTHLY_COST_TEXT.typeLabel.Fixed, color: 'default' },
  Variable: { text: MONTHLY_COST_TEXT.typeLabel.Variable, color: 'blue' },
};
