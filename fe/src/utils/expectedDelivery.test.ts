import { deliveryDateTo, formatDeliveryWindow, isRangeDelivery } from './expectedDelivery';

describe('expectedDelivery', () => {
  it('giao từ 2 lần trở lên là khoảng ngày', () => {
    expect(isRangeDelivery(1)).toBe(false);
    expect(isRangeDelivery(null)).toBe(false);
    expect(isRangeDelivery(2)).toBe(true);
  });

  it('ngày giao đến chỉ gửi khi giao từ 2 lần', () => {
    expect(deliveryDateTo(1, '2026-12-20')).toBeNull();
    expect(deliveryDateTo(3, '2026-12-20')).toBe('2026-12-20');
  });

  it('hiển thị một ngày hoặc khoảng ngày', () => {
    expect(formatDeliveryWindow('2026-12-10', null)).toBe('10/12/2026');
    expect(formatDeliveryWindow('2026-12-10', '2026-12-20')).toBe('10/12/2026 – 20/12/2026');
  });
});
