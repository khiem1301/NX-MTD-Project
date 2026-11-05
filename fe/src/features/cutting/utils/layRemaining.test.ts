import type { CuttingLayLine, WorkOrderLine } from '../types';
import { cutBeforeOf, remainingToCut } from './layRemaining';

const line = (over: Partial<WorkOrderLine> = {}): WorkOrderLine => ({
  workOrderLineId: 1,
  orderLineId: 10,
  color: 'Đen',
  sizeCode: 'M',
  productComponentMaterialId: null,
  componentName: '',
  plannedQty: 100,
  cutQty: 60,
  handedQty: 0,
  ...over,
});

const layLine = (over: Partial<CuttingLayLine> = {}): CuttingLayLine => ({
  cuttingLayLineId: 1,
  orderLineId: 10,
  productComponentMaterialId: null,
  componentName: null,
  color: 'Đen',
  sizeCode: 'M',
  ratioValue: 2,
  calculatedQty: 20,
  actualQty: 20,
  adjustReason: null,
  ...over,
});

describe('cutBeforeOf', () => {
  it('bàn mới: đã cắt trước là toàn bộ số đã cắt của lệnh', () => {
    expect(cutBeforeOf(line())).toBe(60);
  });

  it('sửa bàn cũ: trừ số của chính bàn đó, chỉ đúng dòng (cả bộ khác cắt lẻ)', () => {
    const editing = [
      layLine({ actualQty: 22 }),
      layLine({ productComponentMaterialId: 5, actualQty: 7 }),
    ];

    expect(cutBeforeOf(line(), editing)).toBe(38);
  });
});

describe('remainingToCut', () => {
  it('còn cần cắt = kế hoạch − đã cắt trước − số của bàn này; âm là vượt kế hoạch', () => {
    expect(remainingToCut(100, 60, 0)).toBe(40);
    expect(remainingToCut(100, 60, 30)).toBe(10);
    expect(remainingToCut(100, 60, 45)).toBe(-5);
  });
});
