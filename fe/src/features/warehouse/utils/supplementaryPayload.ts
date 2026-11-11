import type { Dayjs } from 'dayjs';
import { API_DATE } from '@/utils/format';
import type { SaveSupplementaryPayload, ShortDelivery, ShortDeliveryLine } from '../types';

/** Giá trị form lập phiếu bổ sung: ngày nhập, ghi chú và số nhận bù từng dòng đơn thiếu. */
export type SupplementaryValues = {
  receiptDate: Dayjs;
  note?: string;
  lines: Record<number, { acceptedQty: number }>;
};

/** Số còn phải nhận bù của một dòng: số giao bù Quản đốc chọn trừ số đã nhận bù. */
export const remainingReplenish = (line: ShortDeliveryLine) =>
  Math.max(0, (line.replenishQty ?? 0) - line.replenishedQty);

/** Payload phiếu bổ sung: chỉ các dòng còn phải bù và có nhập số nhận lớn hơn 0. */
export const toSupplementaryPayload = (
  detail: ShortDelivery,
  values: SupplementaryValues,
): SaveSupplementaryPayload => ({
  shortageRequestId: detail.requestId,
  receiptDate: values.receiptDate.format(API_DATE),
  note: values.note,
  lines: detail.lines
    .filter((l) => remainingReplenish(l) > 0)
    .map((l) => ({
      requestLineId: l.requestLineId,
      acceptedQty: values.lines[l.requestLineId]?.acceptedQty ?? 0,
    }))
    .filter((l) => l.acceptedQty > 0),
});
