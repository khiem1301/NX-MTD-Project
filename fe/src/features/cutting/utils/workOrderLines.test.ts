import type { WorkOrderLine } from '../types';
import { hasDuplicateLines, splitWorkOrderLines, toWorkOrderLines } from './workOrderLines';

describe('toWorkOrderLines', () => {
  it('bỏ dòng cả bộ không có số kế hoạch', () => {
    expect(toWorkOrderLines({ 1: 100, 2: 0, 3: null, 4: 200 })).toEqual([
      { orderLineId: 1, plannedQty: 100 },
      { orderLineId: 4, plannedQty: 200 },
    ]);
  });

  it('chưa nhập gì thì rỗng', () => {
    expect(toWorkOrderLines(undefined, undefined)).toEqual([]);
  });

  it('cùng một dòng đơn có thể vừa cắt cả bộ vừa cắt lẻ', () => {
    expect(
      toWorkOrderLines({ 1: 10 }, [
        { orderLineId: 1, productComponentMaterialId: 9, plannedQty: 3 },
      ]),
    ).toEqual([
      { orderLineId: 1, plannedQty: 10 },
      { orderLineId: 1, productComponentMaterialId: 9, plannedQty: 3 },
    ]);
  });

  it('dòng cắt lẻ đang nhập dở thì chưa tính', () => {
    expect(
      toWorkOrderLines({}, [
        undefined,
        { orderLineId: 1 },
        { orderLineId: 1, productComponentMaterialId: 9 },
        { orderLineId: 1, productComponentMaterialId: 9, plannedQty: 0 },
      ]),
    ).toEqual([]);
  });
});

describe('splitWorkOrderLines', () => {
  const line = (id: number, qty: number, pcm: number | null) =>
    ({ orderLineId: id, plannedQty: qty, productComponentMaterialId: pcm }) as WorkOrderLine;

  it('tách cả bộ và cắt lẻ; sửa lệnh không làm mất dòng nào', () => {
    const { planned, extras } = splitWorkOrderLines([line(1, 10, null), line(1, 3, 9)]);

    expect(planned).toEqual({ 1: 10 });
    expect(extras).toEqual([{ orderLineId: 1, productComponentMaterialId: 9, plannedQty: 3 }]);
    expect(toWorkOrderLines(planned, extras)).toHaveLength(2);
  });
});

describe('hasDuplicateLines', () => {
  it('cùng màu - size và cùng chi tiết khai hai lần thì trùng', () => {
    expect(
      hasDuplicateLines([
        { orderLineId: 1, plannedQty: 5, productComponentMaterialId: 9 },
        { orderLineId: 1, plannedQty: 2, productComponentMaterialId: 9 },
      ]),
    ).toBe(true);
  });

  it('cả bộ và cắt lẻ của cùng màu - size, hay hai chi tiết khác nhau, không trùng', () => {
    expect(
      hasDuplicateLines([
        { orderLineId: 1, plannedQty: 5 },
        { orderLineId: 1, plannedQty: 2, productComponentMaterialId: 9 },
        { orderLineId: 1, plannedQty: 2, productComponentMaterialId: 10 },
      ]),
    ).toBe(false);
  });
});
