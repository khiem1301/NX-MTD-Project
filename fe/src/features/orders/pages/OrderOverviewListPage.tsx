import { Card } from 'antd';
import { useNavigate } from 'react-router-dom';
import { DataTable } from '@/components/common/DataTable';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';
import { QueryError } from '@/components/common/QueryError';
import { TableToolbar } from '@/components/common/TableToolbar';
import { MESSAGES } from '@/constants/messages';
import type { OrderStatus } from '@/constants/orderStatus';
import { ROUTES } from '@/constants/routes';
import { useTableQuery } from '@/hooks/useTableQuery';
import { ORDER_STATUS_OPTIONS, orderListColumns } from '../components/orderListColumns';
import { useOrderOverviews } from '../hooks/useOrders';
import type { OrderOverviewListItem } from '../types';

type Filters = { keyword?: string; status?: OrderStatus };

/** UC09: Quản đốc xem danh sách đơn hàng, không có thành tiền CMT (GBR-02) và không tạo được đơn. */
export function OrderOverviewListPage() {
  const navigate = useNavigate();
  const table = useTableQuery<Filters>({});
  const orders = useOrderOverviews(table.params);

  const columns = orderListColumns<OrderOverviewListItem>({
    onView: (o) => navigate(ROUTES.managerOrderDetail(o.orderId)),
  });

  return (
    <Card title={MESSAGES.orders.ordersTitle}>
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
          </>
        }
      />
      <QueryError error={orders.error} onRetry={() => void orders.refetch()} />
      <DataTable<OrderOverviewListItem>
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
