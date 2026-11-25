import type { MonthlyCostExcelRowPreview } from '../types';
import { blankDraftRow, draftFromPreview } from './monthlyCostDraft';

const preview = (patch: Partial<MonthlyCostExcelRowPreview>): MonthlyCostExcelRowPreview => ({
  rowNumber: 2,
  costCategoryCode: 'utilities',
  costType: ' fixed ',
  productCode: null,
  amount: 1000,
  quantity: null,
  note: null,
  rowStatus: 'Valid',
  errorMessage: null,
  ...patch,
});

describe('monthlyCostDraft', () => {
  it('chuẩn hóa loại chi phí / kiểu thành chữ hoa khớp danh sách chọn', () => {
    const [row] = draftFromPreview([preview({})]);
    expect(row?.costCategoryCode).toBe('UTILITIES');
    expect(row?.costType).toBe('FIXED');
  });

  it('giữ lỗi ô không đọc được, bỏ lỗi nghiệp vụ (máy chủ tính lại)', () => {
    const rows = draftFromPreview([
      preview({ rowStatus: 'Error', errorMessage: 'Cột Số tiền không phải số.', amount: null }),
      preview({ rowNumber: 3, rowStatus: 'Error', errorMessage: 'Thiếu số tiền.' }),
    ]);
    expect(rows[0]?.parseError).toBe('Cột Số tiền không phải số.');
    expect(rows[1]?.parseError).toBeNull();
  });

  it('dòng trống đánh số tiếp sau mọi dòng đang có', () => {
    const rows = draftFromPreview([preview({ rowNumber: 5 }), preview({ rowNumber: 9 })]);
    expect(blankDraftRow(rows).rowNumber).toBe(10);
    expect(blankDraftRow([]).rowNumber).toBe(1);
  });
});
