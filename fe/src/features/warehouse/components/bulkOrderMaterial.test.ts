import type { MaterialOption } from '@/types/lookup';
import { EMPTY_DRAFT, matchesKeyword, type BulkLine } from './bulkOrderMaterial';

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
