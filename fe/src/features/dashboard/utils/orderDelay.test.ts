import { orderDelayOf } from './orderDelay';

describe('orderDelayOf', () => {
  it('quá ngày giao mà đơn đang chạy: trễ hạn, kể cả khi có cờ nguy cơ', () => {
    expect(orderDelayOf({ daysLeft: -3, isDelayRisk: true, status: 'InProduction' })).toBe('late');
    expect(orderDelayOf({ daysLeft: -1, isDelayRisk: false, status: 'PartiallyDelivered' })).toBe(
      'late',
    );
  });

  it('còn hạn nhưng có cờ nguy cơ: nguy cơ trễ', () => {
    expect(orderDelayOf({ daysLeft: 5, isDelayRisk: true, status: 'InProduction' })).toBe('risk');
    expect(orderDelayOf({ daysLeft: 0, isDelayRisk: true, status: 'InProduction' })).toBe('risk');
  });

  it('còn hạn và không có cờ: không cảnh báo', () => {
    expect(orderDelayOf({ daysLeft: 20, isDelayRisk: false, status: 'InProduction' })).toBeNull();
  });

  it('đơn đã đóng thì không cảnh báo dù quá ngày giao', () => {
    expect(orderDelayOf({ daysLeft: -10, isDelayRisk: false, status: 'Delivered' })).toBeNull();
    expect(orderDelayOf({ daysLeft: -10, isDelayRisk: true, status: 'Cancelled' })).toBeNull();
  });
});
