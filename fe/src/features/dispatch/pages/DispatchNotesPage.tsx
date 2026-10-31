import { useState } from 'react';
import { PlusOutlined } from '@ant-design/icons';
import { Button, Card, type TableColumnsType } from 'antd';
import { Can } from '@/components/guards/Can';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatDate, formatDateTime } from '@/utils/format';
import { DispatchCreateModal } from '../components/DispatchCreateModal';
import { DispatchModal } from '../components/DispatchModal';
import { DISPATCH_LABELS, DISPATCH_STATUS_LABEL } from '../constants';
import { useDispatchNotes } from '../hooks/useDispatch';
import type { DispatchNoteListItem } from '../types';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';
import { DetailLink } from '@/components/common/DetailLink';
import { DataTable } from '@/components/common/DataTable';
import { TableToolbar } from '@/components/common/TableToolbar';

type Filters = { keyword?: string; status?: string };

const STATUS_OPTIONS = Object.entries(DISPATCH_STATUS_LABEL).map(([value, l]) => ({
  value,
  label: l.text,
}));

/** UC45–UC47: phiếu xuất hàng kiêm lệnh giao (Quản đốc lập → Thủ kho xuất → Kế toán xác nhận khách nhận). */
export function DispatchNotesPage() {
  const table = useTableQuery<Filters>({});
  const notes = useDispatchNotes(table.params);
  const [viewingId, setViewingId] = useState<number>();
  const [createOpen, setCreateOpen] = useState(false);

  const columns: TableColumnsType<DispatchNoteListItem> = [
    {
      title: DISPATCH_LABELS.code,
      dataIndex: 'dispatchCode',
      render: (v: string, n) => (
        <DetailLink onClick={() => setViewingId(n.dispatchNoteId)}>{v}</DetailLink>
      ),
    },
    { title: DISPATCH_LABELS.order, dataIndex: 'orderCode' },
    { title: DISPATCH_LABELS.customer, dataIndex: 'customerName' },
    { title: DISPATCH_LABELS.deliveryDate, dataIndex: 'deliveryDate', render: formatDate },
    { title: DISPATCH_LABELS.issueQty, dataIndex: 'totalIssueQty', align: 'right' },
    {
      title: DISPATCH_LABELS.receivedQty,
      dataIndex: 'totalReceivedQty',
      align: 'right',
      render: (v: number | null) => v ?? '—',
    },
    { title: DISPATCH_LABELS.createdAt, dataIndex: 'createdAt', render: formatDateTime },
    {
      title: DISPATCH_LABELS.status,
      dataIndex: 'status',
      render: (v: string) => <StatusTag status={v} labels={DISPATCH_STATUS_LABEL} />,
    },
  ];

  return (
    <Card
      title={DISPATCH_LABELS.title}
      extra={
        <Can roles={[ROLES.Manager]}>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>
            {DISPATCH_LABELS.create}
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
              style={{ width: 280 }}
            />
            <FilterSelect
              value={table.filters.status}
              placeholder={DISPATCH_LABELS.status}
              options={STATUS_OPTIONS}
              onChange={(status?: string) => table.setFilters({ status })}
              style={{ width: 180 }}
            />
          </>
        }
      />
      <QueryError error={notes.error} onRetry={() => void notes.refetch()} />
      <DataTable
        rowKey="dispatchNoteId"
        columns={columns}
        dataSource={notes.data?.items}
        loading={notes.isFetching}
        pagination={table.pagination(notes.data?.totalItems)}
      />
      <DispatchModal noteId={viewingId} onClose={() => setViewingId(undefined)} />
      <DispatchCreateModal open={createOpen} onClose={() => setCreateOpen(false)} />
    </Card>
  );
}
