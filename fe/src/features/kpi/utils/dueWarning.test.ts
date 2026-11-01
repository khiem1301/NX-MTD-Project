import { dueWarning } from './dueWarning';

describe('dueWarning', () => {
  it('báo sau ngày giao kèm số ngày trễ', () => {
    expect(dueWarning('2026-11-22', '2026-11-20', 3)).toEqual({ kind: 'after', days: 2 });
  });

  it('báo sát ngày giao, gồm cả mốc đúng số ngày cấu hình và đúng ngày giao', () => {
    expect(dueWarning('2026-11-17', '2026-11-20', 3)).toEqual({ kind: 'near', days: 3 });
    expect(dueWarning('2026-11-20', '2026-11-20', 3)).toEqual({ kind: 'near', days: 0 });
  });

  it('không báo khi còn xa ngày giao', () => {
    expect(dueWarning('2026-11-16', '2026-11-20', 3)).toBeNull();
  });
});
