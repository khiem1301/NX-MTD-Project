import type { Dayjs } from 'dayjs';
import { toDeliveryDatesPayload } from '@/utils/expectedDelivery';
import type { ResolveShortDeliveryPayload } from '../types';

export type ReplenishPlanValues = {
  expectedDeliveryCount: number;
  expectedDeliveryDates?: (Dayjs | null)[];
};

/** Số lần và các ngày giao bù (mỗi lần một ngày) gửi kèm khi Quản đốc chọn giao bù (DEV-ALL-94). */
export const toReplenishPlan = (
  values: ReplenishPlanValues,
): Pick<ResolveShortDeliveryPayload, 'expectedDeliveryCount' | 'expectedDeliveryDates'> => ({
  expectedDeliveryCount: values.expectedDeliveryCount,
  expectedDeliveryDates: toDeliveryDatesPayload(
    values.expectedDeliveryCount,
    values.expectedDeliveryDates,
  ),
});
