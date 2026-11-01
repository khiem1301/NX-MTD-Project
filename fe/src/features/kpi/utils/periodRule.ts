import type { Dayjs } from 'dayjs';

/** Số ngày tối đa của một kỳ chỉ tiêu (gồm hai đầu), khớp với backend. */
export const KPI_MAX_PERIOD_DAYS = 366;

/** Số hàng đạt tối đa của chỉ tiêu cả kỳ và của một ngày, khớp với backend. */
export const KPI_MAX_QTY = 1_000_000;

/** Kỳ hợp lệ khi chưa chọn đủ hai đầu (để rule "bắt buộc" báo) hoặc dài không quá 366 ngày. */
export const isPeriodWithinLimit = (
  period: [Dayjs | null, Dayjs | null] | null | undefined,
): boolean => !period?.[0] || !period[1] || period[1].diff(period[0], 'day') < KPI_MAX_PERIOD_DAYS;

/** Rule của Form.Item kỳ: báo lỗi khi kỳ dài quá 366 ngày. */
export const periodLimitRule = (message: string) => ({
  validator: (_: unknown, value: [Dayjs | null, Dayjs | null] | null | undefined) =>
    isPeriodWithinLimit(value) ? Promise.resolve() : Promise.reject(new Error(message)),
});
