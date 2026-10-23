import { buildMonthGrid, evenSplit, listWorkDays, monthsOfPeriod, rampUp } from './dailyPlan';

describe('listWorkDays', () => {
  it('bỏ Chủ nhật mặc định và gồm cả hai đầu', () => {
    // 2026-10-04 là Chủ nhật
    expect(listWorkDays('2026-10-01', '2026-10-06')).toEqual([
      '2026-10-01', '2026-10-02', '2026-10-03', '2026-10-05', '2026-10-06',
    ]);
  });

  it('giữ Chủ nhật khi không bỏ', () => {
    expect(listWorkDays('2026-10-03', '2026-10-05', false)).toEqual(['2026-10-03', '2026-10-04', '2026-10-05']);
  });

  it('từ sau đến trước thì rỗng', () => {
    expect(listWorkDays('2026-10-05', '2026-10-01')).toEqual([]);
  });
});

describe('rampUp', () => {
  it('tổng đúng, ngày đầu thấp rồi tăng dần tới mức ổn định', () => {
    const days = rampUp(1000, 25);
    expect(days.reduce((a, b) => a + b, 0)).toBe(1000);
    const [d1, d2, d3, d4] = days as [number, number, number, number];
    expect(d1).toBeLessThan(d2);
    expect(d2).toBeLessThan(d3);
    expect(d3).toBeLessThan(d4);
    expect(Math.max(...days.slice(3)) - Math.min(...days.slice(3))).toBeLessThanOrEqual(1);
  });

  it('kỳ ngắn vẫn đúng tổng và mỗi ngày ít nhất 1', () => {
    expect(rampUp(10, 1)).toEqual([10]);
    const two = rampUp(7, 2);
    expect(two.reduce((a, b) => a + b, 0)).toBe(7);
    expect(Math.min(...rampUp(5, 5))).toBeGreaterThanOrEqual(1);
    expect(rampUp(5, 5).reduce((a, b) => a + b, 0)).toBe(5);
  });

  it('không có ngày thì rỗng', () => {
    expect(rampUp(100, 0)).toEqual([]);
  });
});

describe('evenSplit', () => {
  it('chia đều, phần dư vào các ngày cuối', () => {
    expect(evenSplit(10, 3)).toEqual([3, 3, 4]);
    expect(evenSplit(9, 3)).toEqual([3, 3, 3]);
    expect(evenSplit(0, 3)).toEqual([0, 0, 0]);
  });
});

describe('buildMonthGrid', () => {
  it('tháng 01/2021 bắt đầu Thứ sáu: 4 ô trống đầu, như tờ lịch', () => {
    const weeks = buildMonthGrid('2021-01-15');
    expect(weeks).toHaveLength(5);
    expect(weeks[0]).toEqual([null, null, null, null, '2021-01-01', '2021-01-02', '2021-01-03']);
    expect(weeks[1]?.[0]).toBe('2021-01-04');
    expect(weeks[4]).toEqual(['2021-01-25', '2021-01-26', '2021-01-27', '2021-01-28', '2021-01-29', '2021-01-30', '2021-01-31']);
  });

  it('mỗi tuần đủ 7 ô và ngày nằm đúng cột (Chủ nhật = cột cuối)', () => {
    const weeks = buildMonthGrid('2026-10-01');
    expect(weeks.every((w) => w.length === 7)).toBe(true);
    expect(weeks[0]?.[3]).toBe('2026-10-01'); // 01/10/2026 là Thứ năm
    expect(weeks[0]?.[6]).toBe('2026-10-04'); // Chủ nhật
    expect(weeks.flat().filter(Boolean)).toHaveLength(31);
    expect(weeks[weeks.length - 1]?.[5]).toBe('2026-10-31'); // Thứ bảy
  });
});

describe('monthsOfPeriod', () => {
  it('liệt kê các tháng kỳ KPI trải qua', () => {
    expect(monthsOfPeriod('2026-10-26', '2026-12-03')).toEqual(['2026-10-01', '2026-11-01', '2026-12-01']);
    expect(monthsOfPeriod('2026-10-05', '2026-10-30')).toEqual(['2026-10-01']);
  });
});
