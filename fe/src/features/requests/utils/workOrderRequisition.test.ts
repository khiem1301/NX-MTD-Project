import type { WorkOrderMaterialAllowance } from '@/types/workOrder';
import { remainingOf, suggestLines } from './workOrderRequisition';

const row = (over: Partial<WorkOrderMaterialAllowance>): WorkOrderMaterialAllowance => ({
  materialId: 1,
  materialCode: 'V1',
  materialName: 'Kate',
  materialColor: '',
  unit: 'm',
  needQty: 100,
  capQty: 105,
  requestedQty: 0,
  remainingQty: 105,
  ...over,
});

describe('suggestLines', () => {
  it('lấy số hệ thống tính trừ số đã xin, không quá số còn xin được', () => {
    expect(
      suggestLines([
        row({ materialId: 1 }),
        row({ materialId: 2, requestedQty: 30, remainingQty: 75 }),
        row({ materialId: 3, needQty: 10, capQty: 10, requestedQty: 8, remainingQty: 1.5 }),
      ]),
    ).toEqual([
      { materialId: 1, quantity: 100 },
      { materialId: 2, quantity: 70 },
      { materialId: 3, quantity: 1.5 },
    ]);
  });

  it('bỏ vải đã xin đủ số hệ thống tính và vải chưa tính được', () => {
    expect(
      suggestLines([
        row({ requestedQty: 100, remainingQty: 5 }),
        row({ materialId: 2, needQty: null, capQty: null, remainingQty: null }),
      ]),
    ).toEqual([]);
  });

  it('làm tròn xuống 2 số lẻ', () => {
    expect(suggestLines([row({ needQty: 12.345, remainingQty: 20 })])).toEqual([
      { materialId: 1, quantity: 12.34 },
    ]);
  });
});

describe('remainingOf', () => {
  it('trả số còn xin được, vải ngoài lệnh thì undefined', () => {
    const list = [row({ remainingQty: 40 })];
    expect(remainingOf(list, 1)).toBe(40);
    expect(remainingOf(list, 9)).toBeUndefined();
    expect(remainingOf(undefined, 1)).toBeUndefined();
  });
});
