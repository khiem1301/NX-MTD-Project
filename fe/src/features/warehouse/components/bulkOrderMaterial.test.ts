import type { MaterialOption } from '@/types/lookup';
import { EMPTY_DRAFT, matchesKeyword, rowError, type BulkLine } from './bulkOrderMaterial';

const OPTIONS = [
  { materialId: 2, materialCode: 'CHI01', materialName: 'Chỉ may', color: 'Xanh', unit: 'cuộn' },
] as MaterialOption[];
const NEW_LINE: BulkLine = { key: 'new-1', materialId: null, draft: { ...EMPTY_DRAFT } };
const PICKED: BulkLine = { ...NEW_LINE, materialId: 2 };

describe('matchesKeyword', () => {
  it('không có từ khóa thì hiện mọi dòng', () => {
    expect(matchesKeyword(PICKED, '  ', OPTIONS)).toBe(true);
  });

  it('tìm theo mã, tên, màu, không phân biệt hoa thường', () => {
    expect(matchesKeyword(PICKED, 'chi01', OPTIONS)).toBe(true);
    expect(matchesKeyword(PICKED, 'CHỈ', OPTIONS)).toBe(true);
    expect(matchesKeyword(PICKED, 'xanh', OPTIONS)).toBe(true);
    expect(matchesKeyword(PICKED, 'vải', OPTIONS)).toBe(false);
  });

  it('dòng mới chưa chọn NVL luôn hiện', () => {
    expect(matchesKeyword(NEW_LINE, 'vải', OPTIONS)).toBe(true);
  });
});

describe('rowError', () => {
  const line = (draft: Partial<BulkLine['draft']>, materialId: number | null = 2): BulkLine => ({
    ...NEW_LINE,
    materialId,
    draft: { ...EMPTY_DRAFT, requiredQty: 10, ...draft },
  });

  it('dòng hợp lệ không có lỗi', () => {
    expect(rowError(line({}))).toBeNull();
    expect(rowError(line({ requiredQty: 100_000_000 }))).toBeNull();
    expect(
      rowError(line({ requiredQty: 0, customerQty: 5, expectedDeliveryCount: 99 })),
    ).toBeNull();
  });

  it('chưa chọn NVL, hoặc cả hai số bằng 0, hoặc khách cấp mà thiếu số lần giao', () => {
    expect(rowError(line({}, null))).toBe('Vui lòng nhập thông tin này.');
    expect(rowError(line({ requiredQty: 0 }))).toBe(
      'Phải nhập số cần hoặc số khách cấp lớn hơn 0.',
    );
    expect(rowError(line({ customerQty: 5, expectedDeliveryCount: null }))).toBe(
      'Vui lòng nhập thông tin này.',
    );
  });

  it('số lượng vượt giới hạn hoặc quá 3 số lẻ thì báo đúng ô', () => {
    expect(rowError(line({ requiredQty: 100_000_001 }))).toBe('Số cần tối đa 100.000.000.');
    expect(rowError(line({ requiredQty: 1.2345 }))).toBe('Số cần tối đa 3 số lẻ.');
    expect(rowError(line({ customerQty: 1e15, expectedDeliveryCount: 1 }))).toBe(
      'Số khách cấp tối đa 100.000.000.',
    );
  });

  it('số lần giao ngoài 1..99 hoặc không nguyên thì báo; khách không cấp thì bỏ qua', () => {
    const range = 'Số lần giao dự kiến phải là số nguyên từ 1 đến 99.';
    expect(rowError(line({ customerQty: 5, expectedDeliveryCount: 100 }))).toBe(range);
    expect(rowError(line({ customerQty: 5, expectedDeliveryCount: 1.5 }))).toBe(range);
    expect(rowError(line({ customerQty: 0, expectedDeliveryCount: 100 }))).toBeNull();
  });

  it('ghi chú quá 500 ký tự thì báo', () => {
    expect(rowError(line({ note: 'n'.repeat(501) }))).toBe('Ghi chú tối đa 500 ký tự.');
    expect(rowError(line({ note: ` ${'n'.repeat(500)} ` }))).toBeNull();
  });
});
