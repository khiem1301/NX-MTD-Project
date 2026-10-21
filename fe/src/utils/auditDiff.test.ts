import { describeChanges } from './auditDiff';

describe('describeChanges', () => {
  it('liệt kê trường đổi và dòng thêm / xóa / đổi số lượng', () => {
    const before = JSON.stringify({
      DueDate: '2026-11-30',
      Note: null,
      Lines: [
        { orderLineId: 1, color: 'Đỏ', sizeCode: 'S', orderedQty: 10, cmtUnitPrice: 100 },
        { orderLineId: 2, color: 'Đỏ', sizeCode: 'M', orderedQty: 20, cmtUnitPrice: 100 },
      ],
    });
    const after = JSON.stringify({
      DueDate: '2026-12-05',
      Note: null,
      Lines: [
        { orderLineId: 1, color: 'Đỏ', sizeCode: 'S', orderedQty: 15, cmtUnitPrice: 100 },
        { orderLineId: 3, color: 'Xanh', sizeCode: 'M', orderedQty: 5, cmtUnitPrice: 100 },
      ],
    });

    expect(describeChanges(before, after)).toEqual([
      'Ngày giao: 30/11/2026 → 05/12/2026',
      'Đỏ / S: SL 10 → 15',
      'Thêm dòng Xanh / M: 5',
      'Xóa dòng Đỏ / M',
    ]);
  });

  it('JSON hỏng hoặc thiếu thì trả mảng rỗng', () => {
    expect(describeChanges('không phải json', null)).toEqual([]);
    expect(describeChanges(null, null)).toEqual([]);
  });
});
