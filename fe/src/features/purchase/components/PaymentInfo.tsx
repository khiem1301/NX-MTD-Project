import { Descriptions } from 'antd';
import { formatCurrency, formatDate } from '@/utils/format';
import { PAYMENT_METHOD_LABEL, PURCHASE_TEXT } from '../constants';
import type { PurchaseOrderDetail } from '../types';

const text = PURCHASE_TEXT.detail;

/** Thông tin thanh toán của đơn mua đã thanh toán: ngày, số tiền, hình thức, ghi chú. */
export function PaymentInfo({ order: data }: { order: PurchaseOrderDetail }) {
  return (
    <Descriptions bordered size="small" column={{ xs: 1, sm: 2 }}>
      <Descriptions.Item label={text.paymentDate}>{formatDate(data.paymentDate)}</Descriptions.Item>
      <Descriptions.Item label={text.paidAmount}>
        {formatCurrency(data.paidAmount)}
      </Descriptions.Item>
      <Descriptions.Item label={text.paymentMethod}>
        {data.paymentMethod ? PAYMENT_METHOD_LABEL[data.paymentMethod] : '—'}
      </Descriptions.Item>
      <Descriptions.Item label={text.paymentNote} span={2}>
        {data.paymentNote ?? '—'}
      </Descriptions.Item>
    </Descriptions>
  );
}
