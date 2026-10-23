import { useState } from 'react';
import { Button, Card, Input, Select, Space, Table, type TableColumnsType } from 'antd';
import { Can } from '@/components/guards/Can';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatDate, formatDateTime } from '@/utils/format';
import { DispatchCreateModal } from '../components/DispatchCreateModal';
import { DispatchDrawer } from '../components/DispatchDrawer';
import { DISPATCH_LABELS, DISPATCH_STATUS_LABEL } from '../constants';
import { useDispatchNotes } from '../hooks/useDispatch';
import type { DispatchNoteListItem } from '../types';

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
      render: (v: string, n) => <a onClick={() => setViewingId(n.dispatchNoteId)}>{v}</a>,
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
          <Button type="primary" onClick={() => setCreateOpen(true)}>
            {DISPATCH_LABELS.create}
          </Button>
        </Can>
      }
    >
      <Space style={{ marginBottom: 16 }} wrap>
        <Input.Search
          allowClear
          placeholder={MESSAGES.common.search}
          onSearch={(keyword) => table.setFilters({ keyword: keyword || undefined })}
          style={{ width: 280 }}
        />
        <Select
          allowClear
          placeholder={DISPATCH_LABELS.status}
          options={STATUS_OPTIONS}
          onChange={(status?: string) => table.setFilters({ status })}
          style={{ width: 180 }}
        />
      </Space>
      <QueryError error={notes.error} onRetry={() => void notes.refetch()} />
      <Table
        rowKey="dispatchNoteId"
        columns={columns}
        dataSource={notes.data?.items}
        loading={notes.isFetching}
        pagination={table.pagination(notes.data?.totalItems)}
        locale={{ emptyText: MESSAGES.common.empty }}
      />
      <DispatchDrawer noteId={viewingId} onClose={() => setViewingId(undefined)} />
      <DispatchCreateModal open={createOpen} onClose={() => setCreateOpen(false)} />
    </Card>
  );
}
