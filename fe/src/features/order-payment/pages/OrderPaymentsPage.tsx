import { Card, Space, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { EyeOutlined } from '@ant-design/icons';
import { useSearchParams } from 'react-router-dom';
import { DataTable } from '@/components/common/DataTable';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';
import { IconAction } from '@/components/common/IconAction';
import { PageCard } from '@/components/common/PageCard';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatCurrency } from '@/utils/format';
import { OrderPaymentModal } from '../components/OrderPaymentModal';
import {
  ORDER_PAYMENT_STATE_LABEL,
  ORDER_PAYMENT_STATE_OPTIONS,
  ORDER_PAYMENT_TEXT,
} from '../constants';
import { useOrderPayments } from '../hooks/useOrderPayment';
import type { OrderPaymentListItem, OrderPaymentState } from '../types';
import { remainingLabel } from '../utils/remaining';

type Filters = { keyword?: string; state?: OrderPaymentState };

const text = ORDER_PAYMENT_TEXT.columns;

/** Kế toán: danh sách đơn hàng khách kèm giá trị chốt, đã thanh toán và còn lại (DEV-ALL-118). */
export function OrderPaymentsPage() {
  const [, setSearchParams] = useSearchParams();
  const table = useTableQuery<Filters>({});
  const orders = useOrderPayments(table.params);
  const open = (id: number) => setSearchParams({ id: String(id) });

  const columns: ColumnsType<OrderPaymentListItem> = [
    { title: text.order, dataIndex: 'orderCode' },
    { title: text.customer, dataIndex: 'customerName' },
    {
      title: text.finalAmount,
      dataIndex: 'finalAmount',
      align: 'right',
      render: (v: number | null) => (v === null ? '—' : formatCurrency(v)),
    },
    {
      title: text.paid,
      dataIndex: 'paidAmount',
      align: 'right',
      render: (v: number) => formatCurrency(v),
    },
    {
      title: text.remaining,
      dataIndex: 'remaining',
      render: (v: number | null) => (
        <Typography.Text type={v !== null && v < 0 ? 'danger' : undefined}>
          {remainingLabel(v)}
        </Typography.Text>
      ),
    },
    {
      title: text.state,
      dataIndex: 'state',
      render: (v: OrderPaymentState) => <StatusTag status={v} labels={ORDER_PAYMENT_STATE_LABEL} />,
    },
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, r) => (
        <IconAction
          title={MESSAGES.common.view}
          icon={<EyeOutlined />}
          onClick={() => open(r.orderId)}
        />
      ),
    },
  ];

  return (
    <PageCard title={ORDER_PAYMENT_TEXT.title} description={ORDER_PAYMENT_TEXT.subtitle}>
      <Card size="small">
        <Space direction="vertical" style={{ width: '100%' }}>
          <Space wrap>
            <DebouncedSearch
              allowClear
              placeholder={ORDER_PAYMENT_TEXT.search}
              style={{ width: 340 }}
              onSearch={(value) => table.setFilters({ keyword: value.trim() || undefined })}
            />
            <FilterSelect
              value={table.filters.state}
              placeholder={ORDER_PAYMENT_TEXT.allStates}
              style={{ width: 220 }}
              options={ORDER_PAYMENT_STATE_OPTIONS}
              onChange={(state: OrderPaymentState | undefined) => table.setFilters({ state })}
            />
          </Space>
          <QueryError error={orders.error} onRetry={() => void orders.refetch()} />
          <DataTable<OrderPaymentListItem>
            rowKey="orderId"
            columns={columns}
            dataSource={orders.data?.items}
            loading={orders.isFetching}
            pagination={table.pagination(orders.data?.totalItems)}
            scroll={{ x: 'max-content' }}
          />
        </Space>
      </Card>
      <OrderPaymentModal />
    </PageCard>
  );
}
