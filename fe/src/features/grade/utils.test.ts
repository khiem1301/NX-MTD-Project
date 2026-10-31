import { levelsAround } from './utils';
import type { GradeLevel } from './types';

const level = (
  id: number,
  effectiveDate: string,
  monthlyAmount: number,
  isCurrent = false,
): GradeLevel => ({
  gradeLevelId: id,
  gradeCode: 'D',
  monthlyAmount,
  effectiveDate,
  isCurrent,
});

// Bậc D: 0đ từ 01/01, 20đ từ 31/10 (đang áp dụng), 0đ đã hẹn 04/11.
const LEVELS = [
  level(1, '2026-01-01', 0),
  level(2, '2026-10-31', 20, true),
  level(3, '2026-11-04', 0),
];

describe('levelsAround', () => {
  it('ngày đã có mức hẹn thì trả về mức đó để thay thế', () => {
    const r = levelsAround(LEVELS, '2026-11-04');
    expect(r.sameDay?.gradeLevelId).toBe(3);
    expect(r.before?.gradeLevelId).toBe(2);
  });

  it('ngày nằm giữa mức đang áp dụng và mức đã hẹn: không có mức trùng, mức áp dụng là mức hiện tại', () => {
    const r = levelsAround(LEVELS, '2026-11-02');
    expect(r.sameDay).toBeUndefined();
    expect(r.inEffect?.gradeLevelId).toBe(2);
  });

  it('ngày sau mọi mức đã hẹn: mức liền trước là mức hẹn xa nhất', () => {
    const r = levelsAround(LEVELS, '2026-12-01');
    expect(r.sameDay).toBeUndefined();
    expect(r.before?.gradeLevelId).toBe(3);
  });

  it('bậc chưa có mức nào', () => {
    expect(levelsAround([], '2026-11-02')).toEqual({
      sameDay: undefined,
      inEffect: undefined,
      before: undefined,
    });
  });
});
