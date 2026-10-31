import { Button, Card, Space, Tag, type TableColumnsType } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import {
  MATERIAL_STATUS_TAG,
  ORDER_STATUS_LABEL,
  ORDER_STATUS_TAG,
  type OrderStatus,
} from '@/constants/orderStatus';
import { MESSAGES } from '@/constants/messages';
import { ROUTES } from '@/constants/routes';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatCurrency, formatDate, formatQty } from '@/utils/format';
import { useCustomers, useOrders } from '../hooks/useOrders';
import type { OrderListItem } from '../types';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';
import { DetailLink } from '@/components/common/DetailLink';
import { useNavigate } from 'react-router-dom';
import { DataTable } from '@/components/common/DataTable';
import { TableToolbar } from '@/components/common/TableToolbar';

type Filters = { keyword?: string; status?: OrderStatus; customerId?: number };

const STATUS_OPTIONS = (Object.keys(ORDER_STATUS_LABEL) as OrderStatus[]).map((v) => ({
  value: v,
  label: ORDER_STATUS_LABEL[v],
}));

/** UC09: danh sách đơn hàng (chỉ Admin vì có thành tiền CMT, D26). */
export function OrdersPage() {
  const navigate = useNavigate();
  const table = useTableQuery<Filters>({});
  const orders = useOrders(table.params);
  const customers = useCustomers();

  const columns: TableColumnsType<OrderListItem> = [
    {
      title: MESSAGES.orders.orderCode,
      dataIndex: 'orderCode',
      render: (v: string, o) => (
        <DetailLink onClick={() => navigate(ROUTES.orderDetail(o.orderId))}>{v}</DetailLink>
      ),
    },
    { title: MESSAGES.orders.customer, dataIndex: 'customerName' },
    { title: MESSAGES.orders.productCode, dataIndex: 'productCodes', ellipsis: true },
    { title: MESSAGES.orders.receivedDate, dataIndex: 'receivedDate', render: formatDate },
    { title: MESSAGES.orders.dueDate, dataIndex: 'dueDate', render: formatDate },
    { title: MESSAGES.orders.totalQty, dataIndex: 'totalQty', align: 'right', render: formatQty },
    {
      title: MESSAGES.orders.totalAmount,
      dataIndex: 'totalAmount',
      align: 'right',
      render: formatCurrency,
    },
    {
      title: MESSAGES.orders.status,
      dataIndex: 'status',
      render: (v: string, o) => (
        <Space size={4}>
          <StatusTag status={v} labels={ORDER_STATUS_TAG} />
          {o.isDelayRisk && <Tag color="red">{MESSAGES.orders.delayRisk}</Tag>}
        </Space>
      ),
    },
    {
      title: MESSAGES.orders.materialStatus,
      dataIndex: 'materialStatus',
      render: (v: string) => <StatusTag status={v} labels={MATERIAL_STATUS_TAG} />,
    },
  ];

  return (
    <Card
      title={MESSAGES.orders.ordersTitle}
      extra={
        <Button type="primary" icon={<PlusOutlined />} onClick={() => navigate(ROUTES.orderNew)}>
          {MESSAGES.orders.createOrder}
        </Button>
      }
    >
      <TableToolbar
        filters={
          <>
            <DebouncedSearch
              allowClear
              placeholder={MESSAGES.common.search}
              onSearch={(keyword) => table.setFilters({ keyword: keyword.trim() || undefined })}
              style={{ width: 240 }}
            />
            <FilterSelect<OrderStatus>
              value={table.filters.status}
              placeholder={MESSAGES.orders.status}
              style={{ width: 170 }}
              options={STATUS_OPTIONS}
              onChange={(status) => table.setFilters({ status })}
            />
            <FilterSelect<number>
              value={table.filters.customerId}
              showSearch
              optionFilterProp="label"
              placeholder={MESSAGES.orders.customer}
              style={{ width: 220 }}
              options={(customers.data ?? []).map((c) => ({
                value: c.customerId,
                label: c.customerName,
              }))}
              onChange={(customerId) => table.setFilters({ customerId })}
            />
          </>
        }
      />
      <QueryError error={orders.error} onRetry={() => void orders.refetch()} />
      <DataTable<OrderListItem>
        rowKey="orderId"
        loading={orders.isFetching}
        columns={columns}
        dataSource={orders.data?.items ?? []}
        pagination={table.pagination(orders.data?.totalItems)}
        scroll={{ x: 'max-content' }}
      />
    </Card>
  );
}
