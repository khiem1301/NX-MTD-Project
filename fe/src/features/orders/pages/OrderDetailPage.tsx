import { useState } from 'react';
import {
  Alert,
  Button,
  Descriptions,
  Result,
  Space,
  Spin,
  Tabs,
  Tag,
  Tooltip,
  Typography,
} from 'antd';
import { ConfirmAction } from '@/components/common/ConfirmAction';
import { useNavigate, useParams } from 'react-router-dom';
import { ORDER_DETAIL_TABS } from '@/app/orderDetailTabs';
import { AuditHistoryTable } from '@/components/common/AuditHistoryTable';
import { NoteModal } from '@/components/common/NoteModal';
import { PageCard } from '@/components/common/PageCard';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { ERROR_CODES } from '@/constants/errorCodes';
import { MESSAGES } from '@/constants/messages';
import {
  MATERIAL_STATUS_TAG,
  ORDER_LIMITED_EDIT_STATUSES,
  ORDER_STATUS_TAG,
} from '@/constants/orderStatus';
import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';
import { usePermission } from '@/hooks/usePermission';
import type { ApiError } from '@/types/api';
import { formatCurrency, formatDate, formatDateTime, formatQty } from '@/utils/format';
import { OrderReadOnlyGrid } from '../components/OrderReadOnlyGrid';
import { useCancelOrder, useOrder, useStartProduction, useStopOrder } from '../hooks/useOrders';
import { blocksTotals, fromLines } from '../utils/orderGrid';

type Pending = 'cancel' | 'stop' | null;

/**
 * UC09: chi tiết đơn hàng là trang riêng (cùng cách với tạo / sửa đơn, vì lưới số lượng và các tab quá rộng cho Modal).
 * Nút Sửa / Hủy / Dừng theo trạng thái; Hủy chỉ khi Mới, Dừng bắt buộc lý do.
 */
export function OrderDetailPage() {
  const { id } = useParams();
  const orderId = Number(id);
  const navigate = useNavigate();
  const { data, isLoading, error, refetch } = useOrder(Number.isInteger(orderId) ? orderId : null);
  const cancel = useCancelOrder(orderId);
  const stop = useStopOrder(orderId);
  const start = useStartProduction(orderId);
  const { hasRole } = usePermission();
  const [pending, setPending] = useState<Pending>(null);

  if (isLoading) return <Spin />;
  if ((error as ApiError | null)?.errorCode === ERROR_CODES.CommonNotFound) {
    return <Result status="404" subTitle={MESSAGES.common.notFound} />;
  }
  if (error) return <QueryError error={error} onRetry={() => void refetch()} />;
  if (!data) return null;

  const canEdit = data.status === 'New' || ORDER_LIMITED_EDIT_STATUSES.includes(data.status);
  const canStop = ORDER_LIMITED_EDIT_STATUSES.includes(data.status);
  const totals = blocksTotals(fromLines(data.lines));

  const submit = (note: string | null) => {
    const onDone = { onSettled: () => setPending(null) };
    if (pending === 'cancel') cancel.mutate({ reason: note, rowVer: data.rowVer }, onDone);
    if (pending === 'stop') stop.mutate({ reason: note ?? '', rowVer: data.rowVer }, onDone);
  };

  const tabs = [
    {
      key: 'grid',
      label: MESSAGES.orders.gridTab,
      children: <OrderReadOnlyGrid lines={data.lines} />,
    },
    ...ORDER_DETAIL_TABS.map((t) => ({
      key: t.key,
      label: t.label,
      children: <t.Component orderId={data.orderId} />,
    })),
    {
      key: 'history',
      label: MESSAGES.common.history,
      children: <AuditHistoryTable history={data.history} />,
    },
  ];

  return (
    <PageCard
      onBack={() => navigate(ROUTES.orders)}
      title={
        <Space size={8} wrap>
          {data.orderCode}
          <StatusTag status={data.status} labels={ORDER_STATUS_TAG} />
          {data.isDelayRisk && <Tag color="red">{MESSAGES.orders.delayRisk}</Tag>}
        </Space>
      }
      description={data.customerName}
      extra={
        <Space wrap>
          {canEdit && (
            <Button onClick={() => navigate(ROUTES.orderEdit(data.orderId))}>
              {MESSAGES.common.edit}
            </Button>
          )}
          {data.status === 'New' && (
            <Button danger onClick={() => setPending('cancel')}>
              {MESSAGES.orders.cancelOrder}
            </Button>
          )}
          {canStop && (
            <Button danger onClick={() => setPending('stop')}>
              {MESSAGES.orders.stopOrder}
            </Button>
          )}
          {data.status === 'New' && hasRole(ROLES.Admin) && (
            <Tooltip
              title={
                data.materialStatus === 'Awaiting' ? MESSAGES.orders.startProductionHint : undefined
              }
            >
              <ConfirmAction
                title={MESSAGES.orders.confirmStartProduction}
                onConfirm={() => start.mutate(data.rowVer)}
              >
                <Button
                  type="primary"
                  loading={start.isPending}
                  disabled={data.materialStatus === 'Awaiting'}
                >
                  {MESSAGES.orders.startProduction}
                </Button>
              </ConfirmAction>
            </Tooltip>
          )}
        </Space>
      }
    >
      {data.stopReason && (
        <Alert
          type="warning"
          showIcon
          message={`${MESSAGES.orders.stopReason}: ${data.stopReason}`}
        />
      )}
      <Descriptions
        size="small"
        bordered
        layout="vertical"
        column={{ xs: 2, sm: 3, md: 3, lg: 6, xl: 6, xxl: 6 }}
      >
        <Descriptions.Item label={MESSAGES.orders.receivedDate}>
          {formatDate(data.receivedDate)}
        </Descriptions.Item>
        <Descriptions.Item label={MESSAGES.orders.dueDate}>
          {formatDate(data.dueDate)}
        </Descriptions.Item>
        <Descriptions.Item label={MESSAGES.orders.materialStatus}>
          <StatusTag status={data.materialStatus} labels={MATERIAL_STATUS_TAG} />
        </Descriptions.Item>
        <Descriptions.Item label={MESSAGES.orders.totalQty}>
          <b>{formatQty(totals.qty)}</b>
        </Descriptions.Item>
        <Descriptions.Item label={MESSAGES.orders.totalAmount}>
          <b>{formatCurrency(totals.amount)}</b>
        </Descriptions.Item>
        <Descriptions.Item label={MESSAGES.requests.createdAt}>
          {formatDateTime(data.createdAt)}
        </Descriptions.Item>
      </Descriptions>
      {data.note && (
        <Typography.Paragraph
          type="secondary"
          ellipsis={{ rows: 2, expandable: true }}
          style={{ marginBottom: 0 }}
        >
          {`${MESSAGES.orders.note}: ${data.note}`}
        </Typography.Paragraph>
      )}
      <Tabs size="small" items={tabs} />
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
    </PageCard>
  );
}
