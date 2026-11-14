/** Số lần giao tối đa của một lịch giao (số ô ngày hiện ra); khớp trần ô "số lần giao". */
export const MAX_DELIVERY_DATES = 99;

/** Chữ của các ô ngày dự kiến giao dùng chung cho NVL khách cấp, đơn mua, giao bù: mỗi lần giao một ngày. */
export const DELIVERY_TEXT = {
  dateOfTurn: 'Ngày giao lần {0}',
  dateRequired: 'Chọn ngày giao.',
  dateBeforePrevious: 'Ngày giao không được trước ngày giao lần trước.',
} as const;
