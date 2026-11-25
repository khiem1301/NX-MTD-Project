import { negated, signedQty } from './signedQty';

describe('signedQty', () => {
  it('phiếu giao giữ số dương, phiếu thu hồi hiện số âm', () => {
    expect(signedQty('HANDOVER', 60)).toBe(60);
    expect(signedQty('RETRIEVAL', 15)).toBe(-15);
  });

  it('số 0 không thành -0', () => {
    expect(Object.is(signedQty('RETRIEVAL', 0), 0)).toBe(true);
    expect(Object.is(negated(0), 0)).toBe(true);
  });

  it('tổng thu hồi hiện âm', () => {
    expect(negated(15)).toBe(-15);
  });
});
