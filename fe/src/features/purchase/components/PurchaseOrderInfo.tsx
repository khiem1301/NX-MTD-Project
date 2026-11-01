import { Descriptions, Typography } from 'antd';
import { StatusTag } from '@/components/common/StatusTag';
import { formatCurrency, formatDate } from '@/utils/format';
import { GOODS_STATUS_LABEL, PAYMENT_STATUS_LABEL, PURCHASE_TEXT } from '../constants';
import type { PurchaseOrderDetail } from '../types';

const text = PURCHASE_TEXT.detail;

/** Thông tin chung của đơn mua: đơn hàng, NCC, trạng thái hàng / thanh toán, ngày, tổng tiền, ghi chú. */
export function PurchaseOrderInfo({ order: data }: { order: PurchaseOrderDetail }) {
  return (
    <Descriptions bordered size="small" column={{ xs: 1, sm: 2 }}>
      <Descriptions.Item label={PURCHASE_TEXT.columns.order}>
        {data.orderCode ?? PURCHASE_TEXT.columns.stock}
      </Descriptions.Item>
      <Descriptions.Item label={PURCHASE_TEXT.columns.supplier}>
        {data.supplierName ?? '—'}
      </Descriptions.Item>
      <Descriptions.Item label={PURCHASE_TEXT.columns.goodsStatus}>
        <StatusTag status={data.goodsStatus} labels={GOODS_STATUS_LABEL} />
      </Descriptions.Item>
      <Descriptions.Item label={PURCHASE_TEXT.columns.paymentStatus}>
        <StatusTag status={data.paymentStatus} labels={PAYMENT_STATUS_LABEL} />
      </Descriptions.Item>
      <Descriptions.Item label={PURCHASE_TEXT.columns.orderDate}>
        {formatDate(data.orderDate)}
      </Descriptions.Item>
      <Descriptions.Item label={text.expectedDate}>
        {data.expectedDeliveryCount === null
          ? '—'
          : `${data.receivedCount} / ${data.expectedDeliveryCount}`}
      </Descriptions.Item>
      <Descriptions.Item label={text.total}>
        <Typography.Text strong>{formatCurrency(data.totalAmount)}</Typography.Text>
      </Descriptions.Item>
      <Descriptions.Item label={text.note}>{data.note ?? '—'}</Descriptions.Item>
    </Descriptions>
  );
}
