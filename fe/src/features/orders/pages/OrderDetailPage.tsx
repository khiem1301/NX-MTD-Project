import { Alert, Button, Card, Descriptions, Result, Space, Spin, Table, Tabs, Tag } from 'antd';
import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { NoteModal } from '@/components/common/NoteModal';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { ORDER_DETAIL_TABS } from '@/app/orderDetailTabs';
import { ERROR_CODES } from '@/constants/errorCodes';
import { MESSAGES } from '@/constants/messages';
import { MATERIAL_STATUS_TAG, ORDER_LIMITED_EDIT_STATUSES, ORDER_STATUS_TAG } from '@/constants/orderStatus';
import { ROUTES } from '@/constants/routes';
import type { ApiError } from '@/types/api';
import { formatCurrency, formatDate, formatDateTime, formatQty } from '@/utils/format';
import { AuditHistory } from '@/components/common/AuditHistory';
import { useCancelOrder, useOrder, useStopOrder } from '../hooks/useOrders';
import { blockTotal, fromLines } from '../utils/orderGrid';
import type { OrderLine } from '../types';

type Pending = 'cancel' | 'stop' | null;

function ReadOnlyGrid({ lines }: { lines: OrderLine[] }) {
  return (
    <>
      {fromLines(lines).map((block) => {
        const first = lines.find((l) => l.productId === block.productId);
        return (
          <Card key={block.key} size="small" style={{ marginBottom: 12 }}
            title={`${first?.productCode ?? ''} · ${first?.productName ?? ''}`}
            extra={`${MESSAGES.orders.unitPrice}: ${formatCurrency(block.cmtUnitPrice)} · ${MESSAGES.orders.totalQty}: ${formatQty(blockTotal(block))}`}>
            <Table
              size="small"
              rowKey="key"
              pagination={false}
              dataSource={block.rows}
              columns={[
                { title: MESSAGES.orders.color, dataIndex: 'color' },
                ...block.sizes.map((size) => ({
                  title: size,
                  align: 'right' as const,
                  render: (_: unknown, row: (typeof block.rows)[number]) => formatQty(row.cells[size]?.qty ?? null),
                })),
              ]}
            />
          </Card>
        );
      })}
    </>
  );
}

/** UC09: chi tiết đơn hàng. Nút Sửa / Hủy / Dừng theo trạng thái; Hủy chỉ khi Mới, Dừng bắt buộc lý do. */
export function OrderDetailPage() {
  const { id } = useParams();
  const orderId = Number(id);
  const navigate = useNavigate();
  const { data, isLoading, error, refetch } = useOrder(Number.isInteger(orderId) ? orderId : null);
  const cancel = useCancelOrder(orderId);
  const stop = useStopOrder(orderId);
  const [pending, setPending] = useState<Pending>(null);

  if (isLoading) return <Spin />;
  if ((error as ApiError | null)?.errorCode === ERROR_CODES.CommonNotFound) return <Result status="404" subTitle={MESSAGES.common.notFound} />;
  if (error) return <QueryError error={error} onRetry={() => void refetch()} />;
  if (!data) return null;

  const canEdit = data.status === 'New' || ORDER_LIMITED_EDIT_STATUSES.includes(data.status);
  const canStop = ORDER_LIMITED_EDIT_STATUSES.includes(data.status);

  const submit = (note: string | null) => {
    const onDone = { onSettled: () => setPending(null) };
    if (pending === 'cancel') cancel.mutate({ reason: note, rowVer: data.rowVer }, onDone);
    if (pending === 'stop') stop.mutate({ reason: note ?? '', rowVer: data.rowVer }, onDone);
  };

  const tabs = [
    { key: 'grid', label: MESSAGES.orders.gridTab, children: <ReadOnlyGrid lines={data.lines} /> },
    ...ORDER_DETAIL_TABS.map((t) => ({ key: t.key, label: t.label, children: <t.Component orderId={data.orderId} /> })),
    { key: 'history', label: MESSAGES.common.history, children: <AuditHistory history={data.history} /> },
  ];

  return (
    <Card
      title={data.orderCode}
      extra={
        <Space>
          <Button onClick={() => navigate(ROUTES.orders)}>{MESSAGES.common.close}</Button>
          {canEdit && <Button onClick={() => navigate(ROUTES.orderEdit(data.orderId))}>{MESSAGES.common.edit}</Button>}
          {data.status === 'New' && <Button danger onClick={() => setPending('cancel')}>{MESSAGES.orders.cancelOrder}</Button>}
          {canStop && <Button danger onClick={() => setPending('stop')}>{MESSAGES.orders.stopOrder}</Button>}
        </Space>
      }
    >
      {data.stopReason && <Alert type="warning" showIcon message={`${MESSAGES.orders.stopReason}: ${data.stopReason}`} style={{ marginBottom: 16 }} />}
      <Descriptions size="small" column={3} bordered style={{ marginBottom: 16 }}>
        <Descriptions.Item label={MESSAGES.orders.customer}>{data.customerName}</Descriptions.Item>
        <Descriptions.Item label={MESSAGES.orders.receivedDate}>{formatDate(data.receivedDate)}</Descriptions.Item>
        <Descriptions.Item label={MESSAGES.orders.dueDate}>{formatDate(data.dueDate)}</Descriptions.Item>
        <Descriptions.Item label={MESSAGES.orders.status}>
          <Space size={4}>
            <StatusTag status={data.status} labels={ORDER_STATUS_TAG} />
            {data.isDelayRisk && <Tag color="red">{MESSAGES.orders.delayRisk}</Tag>}
          </Space>
        </Descriptions.Item>
        <Descriptions.Item label={MESSAGES.orders.materialStatus}><StatusTag status={data.materialStatus} labels={MATERIAL_STATUS_TAG} /></Descriptions.Item>
        <Descriptions.Item label={MESSAGES.requests.createdAt}>{formatDateTime(data.createdAt)}</Descriptions.Item>
        {data.note && <Descriptions.Item label={MESSAGES.orders.note} span={3}>{data.note}</Descriptions.Item>}
      </Descriptions>
      <Tabs items={tabs} />
      {pending && (
        <NoteModal
          open
          title={pending === 'cancel' ? MESSAGES.orders.cancelOrder : MESSAGES.orders.stopOrder}
          label={pending === 'cancel' ? MESSAGES.orders.cancelReason : MESSAGES.orders.stopReason}
          required={pending === 'stop'}
          danger
          loading={cancel.isPending || stop.isPending}
          onCancel={() => setPending(null)}
          onSubmit={submit}
        />
      )}
    </Card>
  );
}
