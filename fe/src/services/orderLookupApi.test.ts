import type { OrderLineLookup, OrderLookup, ProductLookup } from '@/types/orderLookup';

/** D26: các kiểu tra cứu cho luồng khác không được có trường giá. */
describe('order lookup types', () => {
  it('không có cmtUnitPrice / totalAmount', () => {
    type Forbidden = 'cmtUnitPrice' | 'totalAmount';
    const noPrice: [Extract<keyof OrderLookup, Forbidden>, Extract<keyof OrderLineLookup, Forbidden>, Extract<keyof ProductLookup, Forbidden>] extends [never, never, never] ? true : false = true;
    expect(noPrice).toBe(true);
  });
});
