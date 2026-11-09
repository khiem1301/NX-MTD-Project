import { Button, Select, Space } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs, { type Dayjs } from 'dayjs';
import { DownloadOutlined } from '@ant-design/icons';
import { QueryError } from '@/components/common/QueryError';
import { REVENUE_TEXT } from '../constants';
import { useExportRevenue, useRevenue, useRevenueFilterOptions } from '../hooks/useRevenue';
import type { RevenueScope } from '../api/revenueApi';
import type { RevenueFilter, RevenueSummary } from '../types';
import { formatMonth, API_MONTH, formatCurrency } from '@/utils/format';
import { DataTable } from '@/components/common/DataTable';
import { AppMonthPicker } from '@/components/common/DateInputs';

type Props = {
  scope: RevenueScope;
  filter: RevenueFilter;
  onFilterChange: (filter: RevenueFilter) => void;
};

const columns: ColumnsType<RevenueSummary> = [
  {
    title: REVENUE_TEXT.table.periodMonth,
    dataIndex: 'periodMonth',
    render: (v: string) => formatMonth(v),
  },
  { title: REVENUE_TEXT.table.orderCode, dataIndex: 'orderCode' },
  {
    title: REVENUE_TEXT.table.product,
    key: 'product',
    render: (_, row) => `${row.productCode} — ${row.productName}`,
  },
  {
    title: REVENUE_TEXT.table.finishedQty,
    dataIndex: 'finishedQty',
    align: 'right',
  },
  {
    title: REVENUE_TEXT.table.revenue,
    dataIndex: 'revenue',
    align: 'right',
    render: (v: number) => formatCurrency(v),
  },
];

/** UC27 — bảng doanh thu, lọc theo tháng/đơn hàng/mã hàng, phân trang server-side, xuất Excel (MSG27). */
export function RevenueTable({ scope, filter, onFilterChange }: Props) {
  const revenue = useRevenue(scope, filter);
  const exportRevenue = useExportRevenue(scope);
  const options = useRevenueFilterOptions(scope);

  return (
    <Space direction="vertical" size={12} style={{ width: '100%' }}>
      <Space wrap>
        <AppMonthPicker
          placeholder={REVENUE_TEXT.filter.periodMonth}
          allowClear
          value={filter.periodMonth ? dayjs(filter.periodMonth, API_MONTH) : null}
          onChange={(value: Dayjs | null) =>
            onFilterChange({
              ...filter,
              page: 1,
              periodMonth: value ? value.format(API_MONTH) : undefined,
            })
          }
        />
        <Select
          showSearch
          allowClear
          optionFilterProp="label"
          placeholder={REVENUE_TEXT.filter.orderId}
          style={{ width: 200 }}
          loading={options.isLoading}
          value={filter.orderId}
          options={(options.data?.orders ?? []).map((o) => ({
            value: o.orderId,
            label: o.orderCode,
          }))}
          onChange={(value?: number) => onFilterChange({ ...filter, page: 1, orderId: value })}
        />
        <Select
          showSearch
          allowClear
          optionFilterProp="label"
          placeholder={REVENUE_TEXT.filter.productId}
          style={{ width: 260 }}
          loading={options.isLoading}
          value={filter.productId}
          options={(options.data?.products ?? []).map((p) => ({
            value: p.productId,
            label: `${p.productCode} — ${p.productName}`,
          }))}
          onChange={(value?: number) => onFilterChange({ ...filter, page: 1, productId: value })}
        />
        <Button
          icon={<DownloadOutlined />}
          loading={exportRevenue.isPending}
          onClick={() => exportRevenue.mutate(filter)}
        >
          {REVENUE_TEXT.export}
        </Button>
      </Space>

      <QueryError error={revenue.error} onRetry={() => void revenue.refetch()} />

      <DataTable<RevenueSummary>
        rowKey={(row) => `${row.periodMonth}-${row.orderId}-${row.productId}`}
        size="small"
        columns={columns}
        dataSource={revenue.data?.items}
        loading={revenue.isFetching}
        scroll={{ x: 'max-content' }}
        pagination={{
          current: revenue.data?.page ?? filter.page,
          pageSize: revenue.data?.pageSize ?? filter.pageSize,
          total: revenue.data?.totalItems ?? 0,
          showSizeChanger: true,
          pageSizeOptions: [10, 20, 50],
          showTotal: (total, [from, to]) => `${from}–${to} / ${total} dòng`,
          onChange: (page, pageSize) => onFilterChange({ ...filter, page, pageSize }),
        }}
      />
    </Space>
  );
}
