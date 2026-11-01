import { isStockChanged } from './stockChanged';

describe('isStockChanged', () => {
  it('phiếu đã duyệt hoặc bị trả không có tồn hiện tại thì không báo đổi', () => {
    expect(isStockChanged({ bookQty: 100, currentQty: null })).toBe(false);
  });

  it('tồn hiện tại bằng số sổ thì không đổi', () => {
    expect(isStockChanged({ bookQty: 100, currentQty: 100 })).toBe(false);
  });

  it('tồn hiện tại khác số sổ, kể cả về 0, thì báo đổi', () => {
    expect(isStockChanged({ bookQty: 100, currentQty: 70 })).toBe(true);
    expect(isStockChanged({ bookQty: 100, currentQty: 0 })).toBe(true);
  });
});
