import { formatCurrency } from '@/utils/format';
import { ORDER_PAYMENT_TEXT } from '../constants';

/** Số còn lại (giá trị chốt − đã thanh toán): dương = khách còn phải trả; âm = mình còn phải trả khách; 0 = đã tất toán. */
export const remainingLabel = (remaining: number | null): string => {
  const text = ORDER_PAYMENT_TEXT.detail;
  if (remaining === null) return text.notSet;
  if (remaining === 0) return text.settled;
  return remaining > 0
    ? `${text.customerOwes}: ${formatCurrency(remaining)}`
    : `${text.weOwe}: ${formatCurrency(-remaining)}`;
};
