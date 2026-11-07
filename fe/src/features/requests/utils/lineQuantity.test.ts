import { lineQuantityRule, roundForMaterial } from './lineQuantity';

describe('lineQuantityRule', () => {
  it('Xin NVL vải: lẻ tối đa 2 chữ số, tối thiểu 0,01', () => {
    expect(lineQuantityRule('MaterialRequisition', 'FABRIC')).toEqual({
      decimals: 2,
      min: 0.01,
      max: 9_999_999,
    });
  });

  it('Xin NVL khác vải (chỉ, cúc…): số nguyên', () => {
    expect(lineQuantityRule('MaterialRequisition', 'TRIM')).toEqual({
      decimals: 0,
      min: 1,
      max: 9_999_999,
    });
  });

  it('Xin NVL chưa chọn NVL: coi như số nguyên cho tới khi chọn', () => {
    expect(lineQuantityRule('MaterialRequisition', undefined).decimals).toBe(0);
  });

  it('Xin BTP: số nguyên đến 1.000.000', () => {
    expect(lineQuantityRule('SemiFinishedRequisition', undefined)).toEqual({
      decimals: 0,
      min: 1,
      max: 1_000_000,
    });
  });

  it('Cơ điện xin linh kiện, vật tư máy: số nguyên đến 9.999', () => {
    const rule = { decimals: 0, min: 1, max: 9_999 };
    expect(lineQuantityRule('SpareParts', undefined)).toEqual(rule);
    expect(lineQuantityRule('MachineSupplies', undefined)).toEqual(rule);
  });

  it('Cơ điện xin máy mới: số nguyên đến 99', () => {
    expect(lineQuantityRule('NewMachine', undefined)).toEqual({ decimals: 0, min: 1, max: 99 });
  });

  it('loại khác: lẻ tối đa 3 chữ số', () => {
    expect(lineQuantityRule('DefectiveMaterial', undefined).decimals).toBe(3);
  });
});

describe('roundForMaterial', () => {
  it('vải giữ nguyên số lẻ', () => {
    expect(roundForMaterial(12.34, 'FABRIC')).toBe(12.34);
  });

  it('NVL khác vải làm tròn, tối thiểu 1', () => {
    expect(roundForMaterial(12.6, 'TRIM')).toBe(13);
    expect(roundForMaterial(0.2, 'TRIM')).toBe(1);
  });
});
