import type { HomeMoney } from '../types';

/** Phần trăm thay đổi so với tháng trước; null khi một trong hai tháng chưa có số hoặc tháng trước bằng 0. */
export const percentVsLast = ({ thisMonth, lastMonth }: HomeMoney): number | null =>
  thisMonth === null || lastMonth === null || lastMonth === 0
    ? null
    : Math.round((100 * (thisMonth - lastMonth)) / Math.abs(lastMonth));
