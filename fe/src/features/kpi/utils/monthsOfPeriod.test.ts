import { monthsOfPeriod } from './monthsOfPeriod';

describe('monthsOfPeriod', () => {
  it('trả một tháng khi kỳ nằm gọn trong tháng', () => {
    expect(monthsOfPeriod('2026-10-05', '2026-10-28')).toEqual(['2026-10-01']);
  });

  it('gồm cả tháng đầu và tháng cuối khi kỳ qua năm mới', () => {
    expect(monthsOfPeriod('2026-11-30', '2027-01-02')).toEqual([
      '2026-11-01',
      '2026-12-01',
      '2027-01-01',
    ]);
  });
});
