import type { MaterialOption } from '@/types/lookup';
import {
  applyDraftChange,
  EMPTY_DRAFT,
  matchesKeyword,
  rowError,
  type BulkLine,
} from './bulkOrderMaterial';

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
    // Lịch khách giao khai ở cả đơn (DEV-ALL-95): dòng khách cấp không cần số lần / ngày giao.
    expect(rowError(line({ requiredQty: 0, customerQty: 5 }))).toBeNull();
  });

  it('chưa chọn NVL hoặc cả hai số bằng 0 thì báo', () => {
    expect(rowError(line({}, null))).toBe('Vui lòng nhập thông tin này.');
    expect(rowError(line({ requiredQty: 0 }))).toBe(
      'Phải nhập số cần hoặc số khách cấp lớn hơn 0.',
    );
  });

  it('số lượng vượt giới hạn hoặc quá 3 số lẻ thì báo đúng ô', () => {
    expect(rowError(line({ requiredQty: 100_000_001 }))).toBe('Số cần tối đa 100.000.000.');
    expect(rowError(line({ requiredQty: 1.2345 }))).toBe('Số cần tối đa 3 số lẻ.');
    expect(rowError(line({ customerQty: 1e15 }))).toBe('Số khách cấp tối đa 100.000.000.');
  });

  it('ghi chú quá 500 ký tự thì báo', () => {
    expect(rowError(line({ note: 'n'.repeat(501) }))).toBe('Ghi chú tối đa 500 ký tự.');
    expect(rowError(line({ note: ` ${'n'.repeat(500)} ` }))).toBeNull();
  });
});

describe('applyDraftChange', () => {
  it('chỉ đổi ô được sửa', () => {
    const next = applyDraftChange({ ...EMPTY_DRAFT, requiredQty: 3 }, { customerQty: 5 });
    expect(next).toEqual({ ...EMPTY_DRAFT, requiredQty: 3, customerQty: 5 });
  });
});
