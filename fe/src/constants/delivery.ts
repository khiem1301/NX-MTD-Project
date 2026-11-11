/**
 * Giao từ số lần này trở lên thì ngày dự kiến giao là một khoảng (từ ngày – đến ngày); khớp
 * DocumentDateRules.RangeDeliveryCount ở BE (DEV-ALL-94).
 */
export const RANGE_DELIVERY_COUNT = 2;

/** Chữ của ô ngày dự kiến giao dùng chung cho NVL khách cấp, đơn mua, giao bù. */
export const DELIVERY_TEXT = {
  dateFrom: 'Giao từ ngày',
  dateTo: 'Giao đến ngày',
  dateToRequired: 'Giao từ 2 lần trở lên thì chọn ngày giao đến.',
  dateToBeforeFrom: 'Ngày giao đến không được trước ngày giao từ.',
} as const;
