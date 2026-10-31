import { useState } from 'react';
import { Button, Popconfirm, Progress, Space, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined, EditOutlined } from '@ant-design/icons';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { formatDate, formatQty } from '@/utils/format';
import { KPI_TEXT } from '../constants';
import { useCancelKpiTarget, useKpiTargets } from '../hooks/useKpi';
import { FinishOrderModal } from './FinishOrderModal';
import type { KpiRole, KpiTarget, KpiTargetFilter, KpiTargetStatus } from '../types';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';
import { DataTable } from '@/components/common/DataTable';

type Props = {
  filter: KpiTargetFilter;
  onFilterChange: (filter: KpiTargetFilter) => void;
  /** Quản đốc lập / sửa / hủy; Admin chỉ xem. */
  role: KpiRole;
  onAdd?: () => void;
  onEdit?: (target: KpiTarget) => void;
};

const STATUS_OPTIONS = (
  Object.entries(KPI_TEXT.statusLabel) as [KpiTargetStatus, { text: string }][]
).map(([value, { text }]) => ({ value, label: text }));

/** Chỉ tiêu đạt: xanh; trên 70%: đang tiến triển; còn lại: cảnh báo. Vượt 100% vẫn vẽ đầy thanh. */
const progressStatus = (percent: number): 'success' | 'normal' | 'exception' =>
  percent >= 100 ? 'success' : percent >= 70 ? 'normal' : 'exception';

/** UC19 / UC21 — danh sách chỉ tiêu KPI tổ may kèm thực tế vs chỉ tiêu, lọc theo trạng thái + từ khóa. */
export function KpiTargetTable({ filter, onFilterChange, role, onAdd, onEdit }: Props) {
  const targets = useKpiTargets(filter, role);
  const cancel = useCancelKpiTarget();
  const canManage = role === 'manager';
  const [finishing, setFinishing] = useState<KpiTarget | null>(null);

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
      title: KPI_TEXT.table.actual,
      dataIndex: 'actualFinishedQty',
      align: 'right',
      render: (v: number) => formatQty(v),
    },
    {
      title: KPI_TEXT.table.percent,
      dataIndex: 'percentAchieved',
      width: 150,
      render: (v: number) => (
        <Progress
          percent={Math.min(100, Math.round(v))}
          format={() => `${formatQty(v)}%`}
          status={progressStatus(v)}
          size="small"
        />
      ),
    },
    {
      title: KPI_TEXT.table.status,
      dataIndex: 'status',
      render: (v: KpiTargetStatus) => (
        <Tag color={KPI_TEXT.statusLabel[v].color}>{KPI_TEXT.statusLabel[v].text}</Tag>
      ),
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
        <Space size={0} wrap>
          {canManage && row.status === 'Active' && (
            <>
              <Button type="link" onClick={() => setFinishing(row)}>
                {KPI_TEXT.finish.button}
              </Button>
              <Button type="link" icon={<EditOutlined />} onClick={() => onEdit?.(row)}>
                {KPI_TEXT.edit}
              </Button>
              <Popconfirm
                title={KPI_TEXT.cancelConfirm}
                onConfirm={() => cancel.mutate(row.kpiTargetId)}
              >
                <Button
                  type="link"
                  danger
                  loading={cancel.isPending && cancel.variables === row.kpiTargetId}
                >
                  {KPI_TEXT.cancel}
                </Button>
              </Popconfirm>
            </>
          )}
        </Space>
      ),
    },
  ];

  return (
    <Space direction="vertical" size={12} style={{ width: '100%' }}>
      <Space wrap>
        <DebouncedSearch
          allowClear
          placeholder={MESSAGES.common.search}
          style={{ width: 260 }}
          defaultValue={filter.keyword}
          onSearch={(keyword) =>
            onFilterChange({ ...filter, page: 1, keyword: keyword || undefined })
          }
        />
        <FilterSelect<KpiTargetStatus>
          placeholder={KPI_TEXT.filter.statusPlaceholder}
          style={{ width: 180 }}
          value={filter.status}
          options={STATUS_OPTIONS}
          onChange={(status) => onFilterChange({ ...filter, page: 1, status })}
        />
        {canManage && onAdd && (
          <Button type="primary" icon={<PlusOutlined />} onClick={onAdd}>
            {KPI_TEXT.add}
          </Button>
        )}
      </Space>

      <QueryError error={targets.error} onRetry={() => void targets.refetch()} />
      <FinishOrderModal target={finishing} onClose={() => setFinishing(null)} />

      <DataTable<KpiTarget>
        rowKey="kpiTargetId"
        size="small"
        columns={columns}
        dataSource={targets.data?.items}
        loading={targets.isFetching}
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
