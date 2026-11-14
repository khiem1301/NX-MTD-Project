import type { ReturnableLine } from '../types';
import {
  buildReturnLines,
  decimalsOfUnit,
  lineKey,
  returnableQueryOf,
  suggestedQtys,
} from './returnLines';

const material = (id: number, remaining: number, used: number | null): ReturnableLine => ({
  materialId: id,
  orderLineId: null,
  itemCode: `V${id}`,
  itemName: 'Vải',
  unit: 'm',
  itemColor: '',
  receivedQty: 100,
  usedQty: used,
  returnedQty: 0,
  remainingQty: remaining,
});

const piece: ReturnableLine = {
  ...material(0, 10, 5),
  materialId: null,
  orderLineId: 70,
  unit: 'bộ',
};

describe('lineKey', () => {
  it('NVL theo mã NVL, BTP theo dòng đơn', () => {
    expect(lineKey(material(3, 1, null))).toBe('m3');
    expect(lineKey(piece)).toBe('o70');
  });
});

describe('decimalsOfUnit', () => {
  it.each([
    ['m', 3],
    ['kg', 3],
    ['KG', 3],
    ['cái', 0],
    ['bộ', 0],
    ['cuộn', 0],
  ])('%s → %d số lẻ', (unit, expected) => {
    expect(decimalsOfUnit(unit)).toBe(expected);
  });
});

describe('suggestedQtys', () => {
  it('chỉ tổ cắt, chỉ dòng đã tính được số đã cắt', () => {
    const lines = [material(1, 40, 60), material(2, 30, null)];

    expect(suggestedQtys(lines, true)).toEqual({ m1: 40 });
    expect(suggestedQtys(lines, false)).toEqual({});
  });
});

describe('buildReturnLines', () => {
  it('gộp hàng còn trả được có nhập số với phế phẩm đã chọn, bỏ dòng trống', () => {
    const lines = buildReturnLines(
      { m1: 12, m2: null, o70: 0 },
      [{ materialId: 9, quantity: 2.5 }, { materialId: 8 }, { quantity: 3 }],
      [material(1, 40, 60), material(2, 30, null), piece],
    );

    expect(lines).toEqual([
      { materialId: 1, orderLineId: undefined, returnedQty: 12 },
      { materialId: 9, returnedQty: 2.5 },
    ]);
  });

  it('BTP gửi orderLineId', () => {
    expect(buildReturnLines({ o70: 4 }, undefined, [piece])).toEqual([
      { materialId: undefined, orderLineId: 70, returnedQty: 4 },
    ]);
  });
});

describe('returnableQueryOf', () => {
  it('tổ cắt phải chọn lệnh cắt mới tải NVL', () => {
    expect(returnableQueryOf({ kind: 'MATERIAL', isCutting: true })).toBeUndefined();
    expect(returnableQueryOf({ kind: 'MATERIAL', isCutting: true, workOrderId: 7 })).toEqual({
      warehouseType: 'MATERIAL',
      workOrderId: 7,
    });
  });

  it('tổ may trả NVL không cần chọn gì', () => {
    expect(returnableQueryOf({ kind: 'MATERIAL', isCutting: false })).toEqual({
      warehouseType: 'MATERIAL',
    });
  });

  it('trả BTP phải chọn đơn hàng', () => {
    expect(returnableQueryOf({ kind: 'SEMI_FINISHED', isCutting: false })).toBeUndefined();
    expect(returnableQueryOf({ kind: 'SEMI_FINISHED', isCutting: false, orderId: 4 })).toEqual({
      warehouseType: 'SEMI_FINISHED',
      orderId: 4,
    });
  });
});
