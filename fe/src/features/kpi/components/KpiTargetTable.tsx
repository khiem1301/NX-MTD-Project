import { Button, Input, Select, Space, Table, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { formatDate, formatQty } from '@/utils/format';
import { KPI_TEXT } from '../constants';
import { useKpiTargets } from '../hooks/useKpi';
import type { KpiTarget, KpiTargetFilter, KpiTargetStatus } from '../types';

type Props = {
  filter: KpiTargetFilter;
  onFilterChange: (filter: KpiTargetFilter) => void;
  onViewDaily: (kpiTargetId: number) => void;
};

const STATUS_OPTIONS = (
  Object.entries(KPI_TEXT.statusLabel) as [KpiTargetStatus, { text: string }][]
).map(([value, { text }]) => ({ value, label: text }));

/** UC19 — Quản đốc: danh sách chỉ tiêu KPI tổ may, lọc theo trạng thái + từ khóa. */
export function KpiTargetTable({ filter, onFilterChange, onViewDaily }: Props) {
  const targets = useKpiTargets(filter);

  const columns: ColumnsType<KpiTarget> = [
    {
      title: KPI_TEXT.table.order,
      dataIndex: 'orderCode',
    },
    {
      title: KPI_TEXT.table.product,
      key: 'product',
      render: (_, row) => `${row.productCode} - ${row.productName}`,
    },
    { title: KPI_TEXT.table.sewingTeam, dataIndex: 'sewingTeamName' },
    {
      title: KPI_TEXT.table.period,
      key: 'period',
      render: (_, row) => `${formatDate(row.startDate)} - ${formatDate(row.endDate)}`,
    },
    {
      title: KPI_TEXT.table.targetFinishedQty,
      dataIndex: 'targetFinishedQty',
      align: 'right',
      render: (v: number) => formatQty(v),
    },
    {
      title: KPI_TEXT.table.dailyTarget,
      dataIndex: 'dailyTarget',
      align: 'right',
      render: (v: number) => formatQty(v),
    },
    {
      title: KPI_TEXT.table.status,
      dataIndex: 'status',
      render: (v: KpiTargetStatus) => <Tag color={KPI_TEXT.statusLabel[v].color}>{KPI_TEXT.statusLabel[v].text}</Tag>,
    },
    {
      title: KPI_TEXT.table.result,
      dataIndex: 'result',
      render: (v: KpiTarget['result']) =>
        v ? <Tag color={KPI_TEXT.resultLabel[v].color}>{KPI_TEXT.resultLabel[v].text}</Tag> : '—',
    },
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, row) => (
        <Button type="link" onClick={() => onViewDaily(row.kpiTargetId)}>
          {KPI_TEXT.table.viewDetail}
        </Button>
      ),
    },
  ];

  return (
    <Space direction="vertical" size={12} style={{ width: '100%' }}>
      <Space wrap>
        <Input.Search
          allowClear
          placeholder={MESSAGES.common.search}
          style={{ width: 260 }}
          defaultValue={filter.keyword}
          onSearch={(keyword) => onFilterChange({ ...filter, page: 1, keyword: keyword || undefined })}
        />
        <Select<KpiTargetStatus | undefined>
          allowClear
          placeholder={KPI_TEXT.filter.statusPlaceholder}
          style={{ width: 180 }}
          value={filter.status}
          options={STATUS_OPTIONS}
          onChange={(status) => onFilterChange({ ...filter, page: 1, status })}
        />
      </Space>

      <QueryError error={targets.error} onRetry={() => void targets.refetch()} />

      <Table<KpiTarget>
        rowKey="kpiTargetId"
        size="middle"
        columns={columns}
        dataSource={targets.data?.items}
        loading={targets.isFetching}
        locale={{ emptyText: MESSAGES.common.empty }}
        scroll={{ x: 'max-content' }}
        pagination={{
          current: targets.data?.page ?? filter.page,
          pageSize: targets.data?.pageSize ?? filter.pageSize,
          total: targets.data?.totalItems ?? 0,
          showSizeChanger: true,
          pageSizeOptions: [10, 20, 50],
          showTotal: (total, [from, to]) => `${from}–${to} / ${total} dòng`,
          onChange: (page, pageSize) => onFilterChange({ ...filter, page, pageSize }),
        }}
      />
    </Space>
  );
}
