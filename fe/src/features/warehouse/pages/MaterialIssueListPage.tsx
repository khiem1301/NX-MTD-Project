import { useState } from 'react';
import { PlusOutlined } from '@ant-design/icons';
import { Button, Card, type TableColumnsType } from 'antd';
import { Can } from '@/components/guards/Can';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatDate } from '@/utils/format';
import { IssueCreateModal } from '../components/IssueCreateModal';
import { IssueDetailModal } from '../components/IssueDetailModal';
import { ISSUE_STATUS_LABEL, LABELS, WAREHOUSE_TYPE, WAREHOUSE_TYPE_LABEL } from '../constants';
import { useIssues } from '../hooks/useIssues';
import type { MaterialIssueListItem } from '../types';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';
import { DetailLink } from '@/components/common/DetailLink';
import { DataTable } from '@/components/common/DataTable';
import { TableToolbar } from '@/components/common/TableToolbar';

type Filters = { keyword?: string; status?: string; warehouseType?: string };

const STATUS_OPTIONS = Object.entries(ISSUE_STATUS_LABEL).map(([value, l]) => ({
  value,
  label: l.text,
}));
const TYPE_OPTIONS = [WAREHOUSE_TYPE.MATERIAL, WAREHOUSE_TYPE.SEMI_FINISHED].map((t) => ({
  value: t,
  label: WAREHOUSE_TYPE_LABEL[t],
}));

/** UC40 / UC41: Thủ kho lập phiếu cấp; tổ trưởng / tổ phó thấy phiếu của tổ mình để xác nhận. */
export function MaterialIssueListPage() {
  const table = useTableQuery<Filters>({});
  const issues = useIssues(table.params);
  const [viewingId, setViewingId] = useState<number>();
  const [createOpen, setCreateOpen] = useState(false);

  const columns: TableColumnsType<MaterialIssueListItem> = [
    {
      title: LABELS.issueCode,
      dataIndex: 'issueCode',
      render: (v: string, r) => (
        <DetailLink onClick={() => setViewingId(r.materialIssueId)}>{v}</DetailLink>
      ),
    },
    {
      title: LABELS.warehouseType,
      dataIndex: 'warehouseType',
      render: (v: string) => WAREHOUSE_TYPE_LABEL[v],
    },
    { title: LABELS.order, dataIndex: 'orderCode' },
    { title: LABELS.workOrder, dataIndex: 'workOrderCode', render: (v: string | null) => v ?? '—' },
    { title: LABELS.receivingTeam, dataIndex: 'receivingTeamName' },
    { title: LABELS.issueDate, dataIndex: 'issueDate', render: formatDate },
    { title: LABELS.issuedBy, dataIndex: 'issuedByName' },
    {
      title: LABELS.status,
      dataIndex: 'status',
      render: (v: string) => <StatusTag status={v} labels={ISSUE_STATUS_LABEL} />,
    },
  ];

  return (
    <Card
      title={LABELS.issuesTitle}
      extra={
        <Can roles={[ROLES.WarehouseKeeper]}>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>
            {MESSAGES.common.create}
          </Button>
        </Can>
      }
    >
      <TableToolbar
        filters={
          <>
            <DebouncedSearch
              allowClear
              placeholder={MESSAGES.common.search}
              onSearch={(keyword) => table.setFilters({ keyword: keyword || undefined })}
              style={{ width: 260 }}
            />
            <FilterSelect
              value={table.filters.warehouseType}
              placeholder={LABELS.warehouseType}
              options={TYPE_OPTIONS}
              onChange={(warehouseType?: string) => table.setFilters({ warehouseType })}
              style={{ width: 150 }}
            />
            <FilterSelect
              value={table.filters.status}
              placeholder={LABELS.status}
              options={STATUS_OPTIONS}
              onChange={(status?: string) => table.setFilters({ status })}
              style={{ width: 160 }}
            />
          </>
        }
      />
      <QueryError error={issues.error} onRetry={() => void issues.refetch()} />
      <DataTable
        rowKey="materialIssueId"
        columns={columns}
        dataSource={issues.data?.items}
        loading={issues.isFetching}
        pagination={table.pagination(issues.data?.totalItems)}
        scroll={{ x: true }}
      />
      <IssueDetailModal issueId={viewingId} onClose={() => setViewingId(undefined)} />
      <IssueCreateModal open={createOpen} onClose={() => setCreateOpen(false)} />
    </Card>
  );
}
