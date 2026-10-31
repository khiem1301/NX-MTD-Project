import type { GradeLevel } from './types';

/**
 * Mức của một bậc quanh một ngày hiệu lực (yyyy-MM-dd): sameDay = mức đã hẹn đúng ngày đó (lưu sẽ thay mức này),
 * inEffect = mức đang áp dụng vào ngày đó, before = mức liền trước ngày đó (để tính chênh lệch).
 */
export const levelsAround = (levels: GradeLevel[], date: string) => {
  const byDateDesc = [...levels].sort((x, y) => y.effectiveDate.localeCompare(x.effectiveDate));
  return {
    sameDay: levels.find((l) => l.effectiveDate === date),
    inEffect: byDateDesc.find((l) => l.effectiveDate <= date),
    before: byDateDesc.find((l) => l.effectiveDate < date),
  };
};
