import dayjs from 'dayjs';
import { isPeriodWithinLimit } from './periodRule';

const from = dayjs('2026-11-02');

describe('isPeriodWithinLimit', () => {
  it('chưa chọn đủ hai đầu thì để rule bắt buộc báo', () => {
    expect(isPeriodWithinLimit(undefined)).toBe(true);
    expect(isPeriodWithinLimit([from, null])).toBe(true);
  });

  it('kỳ 366 ngày (gồm hai đầu) được, 367 ngày thì không', () => {
    expect(isPeriodWithinLimit([from, from])).toBe(true);
    expect(isPeriodWithinLimit([from, from.add(365, 'day')])).toBe(true);
    expect(isPeriodWithinLimit([from, from.add(366, 'day')])).toBe(false);
  });
});
