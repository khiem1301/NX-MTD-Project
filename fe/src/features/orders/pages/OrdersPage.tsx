import { Button, Card } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { QueryError } from '@/components/common/QueryError';
import { type OrderStatus } from '@/constants/orderStatus';
import { MESSAGES } from '@/constants/messages';
import { ROUTES } from '@/constants/routes';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatCurrency } from '@/utils/format';
import { ORDER_STATUS_OPTIONS, orderListColumns } from '../components/orderListColumns';
import { useCustomerOptions, useOrders } from '../hooks/useOrders';
import type { OrderListItem } from '../types';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';
import { useNavigate } from 'react-router-dom';
import { DataTable } from '@/components/common/DataTable';
import { TableToolbar } from '@/components/common/TableToolbar';

type Filters = { keyword?: string; status?: OrderStatus; customerId?: number };

/** UC09: danh sách đơn hàng của Admin (có thành tiền CMT, D26). */
export function OrdersPage() {
  const navigate = useNavigate();
  const table = useTableQuery<Filters>({});
  const orders = useOrders(table.params);
  const customers = useCustomerOptions();

  const columns = orderListColumns<OrderListItem>({
    onView: (o) => navigate(ROUTES.orderDetail(o.orderId)),
    onCopy: (o) => navigate(`${ROUTES.orderNew}?copyFrom=${o.orderId}`),
    extraColumns: [
      {
        title: MESSAGES.orders.totalAmount,
        dataIndex: 'totalAmount',
        align: 'right',
        render: formatCurrency,
      },
    ],
  });

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
              options={ORDER_STATUS_OPTIONS}
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
