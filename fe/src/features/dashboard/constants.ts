/** Cảnh báo tiến độ đơn hàng (UC21): đã quá ngày giao thì là Trễ hạn, còn hạn mà dự báo không kịp thì là Nguy cơ trễ. */
export const ORDER_DELAY = {
  LATE: 'late',
  RISK: 'risk',
} as const;

export const ORDER_DELAY_TEXT = {
  late: 'Trễ hạn',
  risk: 'Nguy cơ trễ',
  since: ' từ {0}',
  lateDays: 'Quá hạn {0} ngày',
  daysLeft: 'Còn {0} ngày',
  lateSummary: '{0} đơn đã trễ hạn: {1}',
  riskSummary: '{0} đơn có nguy cơ trễ hạn: {1}',
} as const;
