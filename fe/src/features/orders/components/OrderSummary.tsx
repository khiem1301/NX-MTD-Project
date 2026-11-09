import { Alert, Descriptions, Typography } from 'antd';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { MATERIAL_STATUS_TAG } from '@/constants/orderStatus';
import { formatCurrency, formatDate, formatDateTime, formatQty } from '@/utils/format';
import { blocksTotals, fromLines, linesDelivered } from '../utils/orderGrid';
import type { OrderDisplayLine, OrderOverview } from '../types';

type OrderSummaryProps = {
  order: Omit<OrderOverview, 'lines'> & { lines: OrderDisplayLine[] };
  /** Thành tiền CMT là dữ liệu mật, chỉ Admin xem (GBR-02). */
  showAmount?: boolean;
};

/** Thông tin chung của đơn: lý do dừng, ngày, tình trạng NVL, tổng số lượng, ghi chú. */
export function OrderSummary({ order, showAmount = false }: OrderSummaryProps) {
  const totals = blocksTotals(fromLines(order.lines));
  const delivered = linesDelivered(order.lines);

  return (
    <>
      {order.stopReason && (
        <Alert
          type="warning"
          showIcon
          message={`${MESSAGES.orders.stopReason}: ${order.stopReason}`}
        />
      )}
      <Descriptions
        size="small"
        bordered
        layout="vertical"
        column={{ xs: 2, sm: 3, md: 3, lg: 7, xl: 7, xxl: 7 }}
      >
        <Descriptions.Item label={MESSAGES.orders.receivedDate}>
          {formatDate(order.receivedDate)}
        </Descriptions.Item>
        <Descriptions.Item label={MESSAGES.orders.dueDate}>
          {formatDate(order.dueDate)}
        </Descriptions.Item>
        <Descriptions.Item label={MESSAGES.orders.materialStatus}>
          <StatusTag status={order.materialStatus} labels={MATERIAL_STATUS_TAG} />
        </Descriptions.Item>
        <Descriptions.Item label={MESSAGES.orders.totalQty}>
          <b>{formatQty(totals.qty)}</b>
        </Descriptions.Item>
        <Descriptions.Item label={MESSAGES.orders.deliveredQty}>
          <b>{`${formatQty(delivered)} / ${formatQty(totals.qty)}`}</b>
        </Descriptions.Item>
        {showAmount && (
          <Descriptions.Item label={MESSAGES.orders.totalAmount}>
            <b>{formatCurrency(totals.amount)}</b>
          </Descriptions.Item>
        )}
        <Descriptions.Item label={MESSAGES.requests.createdAt}>
          {formatDateTime(order.createdAt)}
        </Descriptions.Item>
      </Descriptions>
      {order.note && (
        <Typography.Paragraph
          type="secondary"
          ellipsis={{ rows: 2, expandable: true }}
          style={{ marginBottom: 0 }}
        >
          {`${MESSAGES.orders.note}: ${order.note}`}
        </Typography.Paragraph>
      )}
    </>
  );
}
