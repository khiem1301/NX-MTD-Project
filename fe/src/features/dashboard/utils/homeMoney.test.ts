import { percentVsLast } from './homeMoney';

describe('percentVsLast', () => {
  it('tăng và giảm so với tháng trước, làm tròn phần trăm', () => {
    expect(percentVsLast({ thisMonth: 1120, lastMonth: 1000 })).toBe(12);
    expect(percentVsLast({ thisMonth: 900, lastMonth: 1000 })).toBe(-10);
  });

  it('thiếu số một trong hai tháng hoặc tháng trước bằng 0 thì không có phần trăm', () => {
    expect(percentVsLast({ thisMonth: null, lastMonth: 1000 })).toBeNull();
    expect(percentVsLast({ thisMonth: 1000, lastMonth: null })).toBeNull();
    expect(percentVsLast({ thisMonth: 1000, lastMonth: 0 })).toBeNull();
  });
});
