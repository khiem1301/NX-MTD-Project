import type { RequestDetail } from '@/types/request';
import { requestedQtyMap } from './requestedQty';

const request = {
  lines: [
    { materialId: 7, orderLineId: null, quantity: 12.5 },
    { materialId: null, orderLineId: 3, quantity: 40 },
    { materialId: null, orderLineId: null, quantity: 1 },
  ],
} as unknown as RequestDetail;

describe('requestedQtyMap', () => {
  it('phiếu cấp NVL lấy số lượng theo NVL', () => {
    expect(requestedQtyMap(request, true)).toEqual(new Map([[7, 12.5]]));
  });

  it('phiếu cấp BTP lấy số lượng theo dòng đơn', () => {
    expect(requestedQtyMap(request, false)).toEqual(new Map([[3, 40]]));
  });

  it('chưa chọn đơn xin thì không có trần nào', () => {
    expect(requestedQtyMap(null, true).size).toBe(0);
  });
});
