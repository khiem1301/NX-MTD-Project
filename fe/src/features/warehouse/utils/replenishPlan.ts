import type { Dayjs } from 'dayjs';
import { deliveryDateTo } from '@/utils/expectedDelivery';
import { toApiDate, toApiDateOrNull } from '@/utils/format';
import type { ResolveShortDeliveryPayload } from '../types';

export type ReplenishPlanValues = {
  expectedDeliveryCount: number;
  expectedDeliveryDate: Dayjs;
  expectedDeliveryDateTo?: Dayjs | null;
};

/** Số lần, ngày / khoảng ngày giao bù gửi kèm khi Quản đốc chọn giao bù (DEV-ALL-94). */
export const toReplenishPlan = (
  values: ReplenishPlanValues,
): Pick<
  ResolveShortDeliveryPayload,
  'expectedDeliveryCount' | 'expectedDeliveryDate' | 'expectedDeliveryDateTo'
> => ({
  expectedDeliveryCount: values.expectedDeliveryCount,
  expectedDeliveryDate: toApiDate(values.expectedDeliveryDate),
  expectedDeliveryDateTo: toApiDateOrNull(
    deliveryDateTo(values.expectedDeliveryCount, values.expectedDeliveryDateTo),
  ),
});
