import type { OrderLineOption } from '@/types/lookup';
import { pieceKey } from './pieceKey';
import { roundUpQty, suggestFabricQty, suggestPieceLines } from './issueSuggestion';

const line = (
  id: number,
  color: string,
  size: string,
  qty: number,
  code = 'AO-01',
): OrderLineOption => ({
  orderLineId: id,
  productId: 1,
  productCode: code,
  productName: 'Áo',
  color,
  sizeCode: size,
  orderedQty: qty,
  plannedQty: 0,
});

describe('suggestFabricQty', () => {
  it('làm tròn lên 3 số lẻ', () => {
    expect(roundUpQty(12.3401)).toBe(12.341);
    expect(roundUpQty(5)).toBe(5);
  });

  it('không vượt số khả dụng trong kho', () => {
    expect(suggestFabricQty(100, 40)).toBe(40);
    expect(suggestFabricQty(30.1234, 500)).toBe(30.124);
  });

  it('kho hết hoặc không có nhu cầu thì 0', () => {
    expect(suggestFabricQty(10, 0)).toBe(0);
    expect(suggestFabricQty(0, 10)).toBe(0);
  });
});

describe('suggestPieceLines', () => {
  const lines = [
    line(1, 'Đỏ', 'S', 100),
    line(2, 'Đỏ', 'M', 80),
    line(3, 'Xanh', 'S', 50),
    line(4, 'Đỏ', 'S', 60, 'AO-02'),
  ];
  const available = new Map([
    [1, 70],
    [2, 200],
    [3, 50],
    [4, 60],
  ]);
  const filter = { productCode: 'AO-01', color: 'Đỏ' };

  it('theo mã hàng + màu: mọi size, số cấp = min(khả dụng, số đặt)', () => {
    expect(suggestPieceLines(lines, available, filter, new Set())).toEqual([
      { pieceKey: pieceKey(1, null), issuedQty: 70 },
      { pieceKey: pieceKey(2, null), issuedQty: 80 },
    ]);
  });

  it('thêm size thì chỉ lấy size đó', () => {
    expect(suggestPieceLines(lines, available, { ...filter, sizeCode: 'M' }, new Set())).toEqual([
      { pieceKey: pieceKey(2, null), issuedQty: 80 },
    ]);
  });

  it('bỏ dòng hết hàng và dòng đã có trong phiếu', () => {
    const none = new Map<number, number>([[1, 0]]);
    expect(suggestPieceLines(lines, none, filter, new Set())).toEqual([]);
    expect(suggestPieceLines(lines, available, filter, new Set([pieceKey(1, null)]))).toEqual([
      { pieceKey: pieceKey(2, null), issuedQty: 80 },
    ]);
  });
});
