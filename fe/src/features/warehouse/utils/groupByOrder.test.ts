import { groupByOrder } from './groupByOrder';

const row = (orderId: number, orderMaterialId: number) => ({ orderId, orderMaterialId });

describe('groupByOrder', () => {
  it('gộp các NVL của cùng một đơn hàng thành một hàng, giữ thứ tự xuất hiện', () => {
    const groups = groupByOrder([row(1, 10), row(2, 20), row(1, 11)]);

    expect(groups.map((g) => g.orderId)).toEqual([1, 2]);
    expect(groups[0]?.lines.map((l) => l.orderMaterialId)).toEqual([10, 11]);
    expect(groups[1]?.lines).toHaveLength(1);
  });

  it('không có dữ liệu thì trả mảng rỗng', () => {
    expect(groupByOrder(undefined)).toEqual([]);
  });
});
