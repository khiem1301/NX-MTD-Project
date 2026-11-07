import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import type { Dayjs } from 'dayjs';
import { DataTable } from '@/components/common/DataTable';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { AppDateRangeFields } from '@/components/common/DateInputs';
import { FilterSelect } from '@/components/common/FilterSelect';
import { PageCard } from '@/components/common/PageCard';
import { QueryError } from '@/components/common/QueryError';
import { TableToolbar } from '@/components/common/TableToolbar';
import { useTableQuery } from '@/hooks/useTableQuery';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import { AUDIT_ACTION, auditTableLabel } from '@/utils/auditLabels';
import { API_DATE } from '@/utils/format';
import { AuditLogDetailModal } from '../components/AuditLogDetailModal';
import { auditLogColumns } from '../components/auditLogColumns';
import type { AuditRow } from '../types';

const BASE = '/api/v1/admin/audit-logs';

const get = async <T,>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;

type Filters = {
  keyword?: string;
  tableName?: string;
  action?: string;
  fromDate?: string;
  toDate?: string;
};

/** UC22: Admin xem nhật ký thay đổi dữ liệu — ai, lúc nào, đối tượng nào, thay đổi gì, lý do. */
export function AuditLogPage() {
  const table = useTableQuery<Filters>({});
  const [viewing, setViewing] = useState<AuditRow | null>(null);
  const tables = useQuery({
    queryKey: ['audit-log', 'tables'],
    queryFn: () => get<string[]>(`${BASE}/tables`),
    staleTime: 60_000,
  });
  const logs = useQuery({
    queryKey: ['audit-log', 'list', table.params],
    queryFn: () => get<Paginated<AuditRow>>(BASE, table.params),
  });

  return (
    <PageCard title="Nhật ký thay đổi">
      <TableToolbar
        filters={
          <>
            <DebouncedSearch
              allowClear
              placeholder="Tìm theo lý do / người thực hiện"
              onSearch={(keyword) => table.setFilters({ keyword: keyword.trim() || undefined })}
              style={{ width: 280 }}
            />
            <FilterSelect
              value={table.filters.tableName}
              showSearch
              placeholder="Đối tượng"
              style={{ width: 220 }}
              options={(tables.data ?? []).map((t) => ({ value: t, label: auditTableLabel(t) }))}
              onChange={(tableName?: string) => table.setFilters({ tableName })}
            />
            <FilterSelect
              value={table.filters.action}
              placeholder="Hành động"
              style={{ width: 180 }}
              options={Object.entries(AUDIT_ACTION).map(([value, a]) => ({ value, label: a.text }))}
              onChange={(action?: string) => table.setFilters({ action })}
            />
            <AppDateRangeFields
              allowPartial
              onChange={(range: [Dayjs | null, Dayjs | null] | null) =>
                table.setFilters({
                  fromDate: range?.[0]?.format(API_DATE),
                  toDate: range?.[1]?.format(API_DATE),
                })
              }
            />
          </>
        }
      />
      <QueryError error={logs.error} onRetry={() => void logs.refetch()} />
      <DataTable
        rowKey="auditLogId"
        columns={auditLogColumns(setViewing)}
        dataSource={logs.data?.items}
        loading={logs.isFetching}
        pagination={table.pagination(logs.data?.totalItems)}
      />
      <AuditLogDetailModal row={viewing} onClose={() => setViewing(null)} />
    </PageCard>
  );
}
