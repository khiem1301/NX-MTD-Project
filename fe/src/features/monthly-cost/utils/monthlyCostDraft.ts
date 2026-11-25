import type {
  MonthlyCostDraftRow,
  MonthlyCostExcelRowPreview,
  MonthlyCostRowsPayload,
} from '../types';

/** Lỗi do ô không đọc được (chữ ở ô số...): máy chủ chỉ thấy ô rỗng nên giữ lỗi này đến khi người dùng sửa dòng. */
const PARSE_ERROR = /^Cột (Số tiền|Số lượng) không phải số/;

const upper = (v: string | null) => (v?.trim() ? v.trim().toUpperCase() : null);

/** Dòng nháp từ kết quả xem trước file: loại chi phí / kiểu viết hoa để khớp danh sách chọn. */
export const draftFromPreview = (
  rows: readonly MonthlyCostExcelRowPreview[],
): MonthlyCostDraftRow[] =>
  rows.map((r) => ({
    rowNumber: r.rowNumber,
    costCategoryCode: upper(r.costCategoryCode),
    costType: upper(r.costType),
    productCode: r.productCode,
    amount: r.amount,
    quantity: r.quantity,
    note: r.note,
    parseError:
      r.rowStatus === 'Error' && r.errorMessage && PARSE_ERROR.test(r.errorMessage)
        ? r.errorMessage
        : null,
  }));

/** Dòng trống để thêm: số dòng lớn hơn mọi dòng đang có; kiểu mặc định cố định. */
export const blankDraftRow = (rows: readonly MonthlyCostDraftRow[]): MonthlyCostDraftRow => ({
  rowNumber: Math.max(0, ...rows.map((r) => r.rowNumber)) + 1,
  costCategoryCode: null,
  costType: 'FIXED',
  productCode: null,
  amount: null,
  quantity: null,
  note: null,
  parseError: null,
});

export const payloadOf = (
  periodMonth: string,
  rows: readonly MonthlyCostDraftRow[],
): MonthlyCostRowsPayload => ({
  periodMonth,
  rows: [...rows],
});
