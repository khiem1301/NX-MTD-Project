import { useState } from 'react';
import { Card, Descriptions, Space, Tag, Typography, type TableColumnsType } from 'antd';
import { useQuery } from '@tanstack/react-query';
import type { Dayjs } from 'dayjs';
import { QueryError } from '@/components/common/QueryError';
import { useTableQuery } from '@/hooks/useTableQuery';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import { API_DATE, formatDateTime } from '@/utils/format';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';
import { DetailModal } from '@/components/common/DetailModal';
import { DetailLink } from '@/components/common/DetailLink';
import { DataTable } from '@/components/common/DataTable';
import { AppRangePicker } from '@/components/common/DateInputs';
import { TableToolbar } from '@/components/common/TableToolbar';

type AuditRow = {
  auditLogId: number;
  tableName: string;
  recordId: number;
  action: string;
  oldValues: string | null;
  newValues: string | null;
  reason: string | null;
  changedBy: number | null;
  changedByName: string | null;
  changedAt: string;
};

const BASE = '/api/v1/admin/audit-logs';
const ACTIONS = [
  'INSERT',
  'UPDATE',
  'DELETE',
  'APPROVE',
  'REJECT',
  'CONFIRM',
  'LOCK',
  'UNLOCK',
  'LOGIN',
];
const ACTION_COLOR: Record<string, string> = {
  INSERT: 'green',
  UPDATE: 'blue',
  DELETE: 'red',
  APPROVE: 'cyan',
  REJECT: 'orange',
  CONFIRM: 'geekblue',
  LOCK: 'purple',
  UNLOCK: 'purple',
  LOGIN: 'default',
};

const get = async <T,>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;

const pretty = (json: string | null) => {
  if (!json) return '—';
  try {
    return JSON.stringify(JSON.parse(json), null, 2);
  } catch {
    return json;
  }
};

type Filters = {
  keyword?: string;
  tableName?: string;
  action?: string;
  fromDate?: string;
  toDate?: string;
};

/** UC22: Admin xem nhật ký thay đổi dữ liệu — ai, lúc nào, bảng nào, giá trị cũ / mới, lý do. */
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

  const columns: TableColumnsType<AuditRow> = [
    {
      title: 'Lúc',
      dataIndex: 'changedAt',
      width: 160,
      render: (v: string, r) => (
        <DetailLink onClick={() => setViewing(r)}>{formatDateTime(v)}</DetailLink>
      ),
    },
    {
      title: 'Người thực hiện',
      dataIndex: 'changedByName',
      render: (v: string | null) => v ?? 'Hệ thống',
    },
    { title: 'Bảng', dataIndex: 'tableName' },
    { title: 'Mã bản ghi', dataIndex: 'recordId', align: 'right' },
    {
      title: 'Hành động',
      dataIndex: 'action',
      render: (v: string) => <Tag color={ACTION_COLOR[v] ?? 'default'}>{v}</Tag>,
    },
    { title: 'Lý do', dataIndex: 'reason', render: (v: string | null) => v ?? '—' },
  ];

  return (
    <Card title="Nhật ký thay đổi">
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
              placeholder="Bảng"
              style={{ width: 200 }}
              options={(tables.data ?? []).map((t) => ({ value: t, label: t }))}
              onChange={(tableName?: string) => table.setFilters({ tableName })}
            />
            <FilterSelect
              value={table.filters.action}
              placeholder="Hành động"
              style={{ width: 160 }}
              options={ACTIONS.map((a) => ({ value: a, label: a }))}
              onChange={(action?: string) => table.setFilters({ action })}
            />
            <AppRangePicker
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
        columns={columns}
        dataSource={logs.data?.items}
        loading={logs.isFetching}
        pagination={table.pagination(logs.data?.totalItems)}
      />
      <DetailModal
        open={viewing !== null}
        onClose={() => setViewing(null)}
        title="Chi tiết thay đổi"
        size="md"
      >
        {viewing && (
          <Space direction="vertical" size={16} style={{ width: '100%' }}>
            <Descriptions column={2} size="small" bordered>
              <Descriptions.Item label="Lúc">{formatDateTime(viewing.changedAt)}</Descriptions.Item>
              <Descriptions.Item label="Người thực hiện">
                {viewing.changedByName ?? 'Hệ thống'}
              </Descriptions.Item>
              <Descriptions.Item label="Bảng / bản ghi">{`${viewing.tableName} #${viewing.recordId}`}</Descriptions.Item>
              <Descriptions.Item label="Hành động">{viewing.action}</Descriptions.Item>
              <Descriptions.Item label="Lý do" span={2}>
                {viewing.reason ?? '—'}
              </Descriptions.Item>
            </Descriptions>
            <div>
              <Typography.Text strong>Giá trị cũ</Typography.Text>
              <pre
                style={{ background: 'rgba(0,0,0,0.04)', padding: 8, margin: 0, overflow: 'auto' }}
              >
                {pretty(viewing.oldValues)}
              </pre>
            </div>
            <div>
              <Typography.Text strong>Giá trị mới</Typography.Text>
              <pre
                style={{ background: 'rgba(0,0,0,0.04)', padding: 8, margin: 0, overflow: 'auto' }}
              >
                {pretty(viewing.newValues)}
              </pre>
            </div>
          </Space>
        )}
      </DetailModal>
    </Card>
  );
}
