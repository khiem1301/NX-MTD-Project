import type { GradeLevel } from './types';
import dayjs from 'dayjs';

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

export const moneyFormatter = (v: number | string | undefined) =>
  `${v ?? ''}`.replace(/\B(?=(\d{3})+(?!\d))/g, '.');

export const moneyParser = (v: string | undefined) => Number((v ?? '').replace(/\./g, ''));

export const currentLevel = (all: GradeLevel[], code: string) =>
  all.find((l) => l.gradeCode === code && l.isCurrent);

/** Các mức đã hẹn ngày tương lai của một bậc, sớm trước: để Admin biết giá sắp áp dụng. */
export const upcomingLevels = (all: GradeLevel[], code: string) =>
  all
    .filter(
      (l) => l.gradeCode === code && !l.isCurrent && dayjs(l.effectiveDate).isAfter(dayjs(), 'day'),
    )
    .sort((x, y) => x.effectiveDate.localeCompare(y.effectiveDate));
