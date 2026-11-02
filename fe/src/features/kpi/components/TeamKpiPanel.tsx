import { Card } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { FilterSelect } from '@/components/common/FilterSelect';
import { QueryError } from '@/components/common/QueryError';
import { useTableQuery } from '@/hooks/useTableQuery';
import { KPI_TEXT } from '../constants';
import { useTeamKpiTargets } from '../hooks/useKpi';
import type { KpiTarget, KpiTargetStatus } from '../types';
import { KPI_STATUS_OPTIONS, kpiTargetColumns } from './kpiColumns';

const columns = kpiTargetColumns(false);

/** Trang chủ tổ trưởng / tổ phó may: chỉ tiêu KPI được giao cho tổ và tiến độ, mặc định các chỉ tiêu đang theo dõi. */
export function TeamKpiPanel() {
  const table = useTableQuery<{ status?: KpiTargetStatus }>({ status: 'Active' });
  const targets = useTeamKpiTargets(table.params);

  return (
    <Card title={KPI_TEXT.teamPanel.title} style={{ marginTop: 16 }}>
      <FilterSelect<KpiTargetStatus>
        placeholder={KPI_TEXT.filter.statusPlaceholder}
        style={{ width: 220, marginBottom: 16 }}
        value={table.filters.status}
        options={KPI_STATUS_OPTIONS}
        onChange={(status) => table.setFilters({ status })}
      />
      <QueryError error={targets.error} onRetry={() => void targets.refetch()} />
      <DataTable<KpiTarget>
        rowKey="kpiTargetId"
        columns={columns}
        dataSource={targets.data?.items}
        loading={targets.isFetching}
        locale={{ emptyText: KPI_TEXT.teamPanel.empty }}
        pagination={table.pagination(targets.data?.totalItems)}
      />
    </Card>
  );
}
