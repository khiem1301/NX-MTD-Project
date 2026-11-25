import { useState } from 'react';
import { Card, Space } from 'antd';
import { EyeOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import { DataTable } from '@/components/common/DataTable';
import { FilterSelect } from '@/components/common/FilterSelect';
import { IconAction } from '@/components/common/IconAction';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { useTableQuery } from '@/hooks/useTableQuery';
import { KPI_TEXT } from '../constants';
import { useTeamKpiTargets } from '../hooks/useKpi';
import type { KpiTarget, KpiTargetStatus } from '../types';
import { KpiCalendarModal } from './KpiCalendarModal';
import { KPI_STATUS_OPTIONS, kpiTargetColumns } from './kpiColumns';

/** Trang chủ tổ trưởng / tổ phó may: chỉ tiêu KPI được giao cho tổ, tiến độ, và xem KPI theo ngày. */
export function TeamKpiPanel({ compact }: { compact?: boolean } = {}) {
  const table = useTableQuery<{ status?: KpiTargetStatus }>({ status: 'Active' });
  const targets = useTeamKpiTargets(table.params);
  const [calendarOf, setCalendarOf] = useState<KpiTarget | null>(null);

  const openCalendar = (row: KpiTarget) => setCalendarOf(row);

  const columns: ColumnsType<KpiTarget> = [
    ...kpiTargetColumns(false),
    {
      title: MESSAGES.common.actions,
      key: 'action',
      width: 72,
      render: (_, row) => (
        <Space size={0}>
          <IconAction
            title={KPI_TEXT.teamPanel.viewDaily}
            icon={<EyeOutlined />}
            onClick={(e) => {
              e.stopPropagation();
              openCalendar(row);
            }}
          />
        </Space>
      ),
    },
  ];

  return (
    <Card title={KPI_TEXT.teamPanel.title} size={compact ? 'small' : 'default'} style={{ height: '100%' }}>
      <FilterSelect<KpiTargetStatus>
        placeholder={KPI_TEXT.filter.statusPlaceholder}
        style={{ width: 200, marginBottom: 12 }}
        value={table.filters.status}
        options={KPI_STATUS_OPTIONS}
        onChange={(status) => table.setFilters({ status })}
      />
      <QueryError error={targets.error} onRetry={() => void targets.refetch()} />
      <DataTable<KpiTarget>
        rowKey="kpiTargetId"
        size="small"
        columns={columns}
        dataSource={targets.data?.items}
        loading={targets.isFetching}
        locale={{ emptyText: KPI_TEXT.teamPanel.empty }}
        scroll={{ x: 'max-content' }}
        pagination={table.pagination(targets.data?.totalItems)}
        onRow={(row) => ({
          onClick: () => openCalendar(row),
          style: { cursor: 'pointer' },
        })}
      />
      <KpiCalendarModal
        target={calendarOf}
        mode="team"
        onClose={() => setCalendarOf(null)}
      />
    </Card>
  );
}
