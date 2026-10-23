import { evenSplit, listWorkDays, rampUp, rowsForPeriod, syncRows } from './dailyPlan';

describe('listWorkDays', () => {
  it('bỏ Chủ nhật mặc định và gồm cả hai đầu kỳ', () => {
    // 2026-10-04 là Chủ nhật
    expect(listWorkDays('2026-10-01', '2026-10-06')).toEqual([
      '2026-10-01', '2026-10-02', '2026-10-03', '2026-10-05', '2026-10-06',
    ]);
  });

  it('giữ Chủ nhật khi không bỏ', () => {
    expect(listWorkDays('2026-10-03', '2026-10-05', false)).toEqual(['2026-10-03', '2026-10-04', '2026-10-05']);
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
  });
});

describe('syncRows / rowsForPeriod', () => {
  it('giữ số đã nhập của ngày còn trong kỳ', () => {
    const rows = syncRows(['2026-10-01', '2026-10-02'], [{ workDate: '2026-10-02', targetQty: 30 }]);
    expect(rows).toEqual([
      { workDate: '2026-10-01', targetQty: null },
      { workDate: '2026-10-02', targetQty: 30 },
    ]);
  });

  it('giữ Chủ nhật đã thêm tay nếu vẫn trong kỳ, bỏ ngày ra ngoài kỳ', () => {
    const rows = rowsForPeriod('2026-10-03', '2026-10-05', [
      { workDate: '2026-10-04', targetQty: 12 },
      { workDate: '2026-10-09', targetQty: 50 },
    ]);
    expect(rows.map((r) => r.workDate)).toEqual(['2026-10-03', '2026-10-04', '2026-10-05']);
    expect(rows[1]?.targetQty).toBe(12);
  });
});
