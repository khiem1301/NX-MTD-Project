import { purchaseOrderTotal } from './purchaseOrderTotal';

describe('purchaseOrderTotal', () => {
  it('cộng số mua nhân đơn giá của mọi dòng', () => {
    expect(
      purchaseOrderTotal([
        { orderedQty: 400, unitPrice: 200 },
        { orderedQty: 2.5, unitPrice: 1000 },
      ]),
    ).toBe(82500);
  });

  it('dòng chưa nhập đủ hoặc chưa có dòng nào thì tính 0', () => {
    expect(purchaseOrderTotal(undefined)).toBe(0);
    expect(purchaseOrderTotal([undefined, { orderedQty: 5 }, { unitPrice: 5 }])).toBe(0);
  });

  it('không giữ sai số của phép tính số thực', () => {
    expect(purchaseOrderTotal([{ orderedQty: 0.1, unitPrice: 0.2 }])).toBe(0.02);
  });
});
