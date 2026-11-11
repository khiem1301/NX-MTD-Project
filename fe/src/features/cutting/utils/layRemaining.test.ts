import type { FabricRequirement } from '@/types/workOrder';
import type { CuttingLayLine, WorkOrderLine } from '../types';
import { cutBeforeOf, remainingToCut, teamFabricBalance } from './layRemaining';

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
  actualQty: 20,
  ...over,
});

const fabric = (over: Partial<FabricRequirement> = {}): FabricRequirement => ({
  materialId: 1,
  materialCode: 'V01',
  materialName: 'Kaki',
  unit: 'm',
  materialColor: 'Đen',
  issuedQty: 100,
  availableQty: 0,
  receivedQty: 100,
  returnedQty: 0,
  usedQty: 100,
  heldQty: 100,
  cutQty: 60,
  remainingQty: 40,
  unmeasuredLayCount: 0,
  ...over,
});

describe('teamFabricBalance', () => {
  it('bàn mới: còn lại = đang giữ − đã dùng; sau bàn này trừ tiếp số mét nhập', () => {
    expect(teamFabricBalance(fabric(), null, 15)).toEqual({
      held: 100,
      usedBefore: 60,
      left: 40,
      thisLay: 15,
      after: 25,
      unmeasuredLays: 0,
    });
  });

  it('sửa bàn cũ cùng vải: không tính trùng số mét của chính bàn đó', () => {
    const b = teamFabricBalance(fabric(), { fabricMaterialId: 1, fabricUsedM: 20 }, 50);
    expect([b.usedBefore, b.left, b.after]).toEqual([40, 60, 10]);
  });

  it('sửa bàn cũ nhưng đổi sang vải khác: không trừ bàn đó', () => {
    expect(teamFabricBalance(fabric(), { fabricMaterialId: 2, fabricUsedM: 20 }, 0).left).toBe(40);
  });

  it('đã dùng nhiều hơn đang giữ: còn lại 0, sau bàn này âm (vượt)', () => {
    const b = teamFabricBalance(fabric({ cutQty: 120 }), null, 5);
    expect([b.left, b.after]).toEqual([0, -5]);
  });
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
