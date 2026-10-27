import { useState } from 'react';
import { Button, Card, Space, Table, type TableColumnsType } from 'antd';
import { Can } from '@/components/guards/Can';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatDate, formatDateTime } from '@/utils/format';
import { ReceiptDetailDrawer } from '../components/ReceiptDetailDrawer';
import { ReceiptFormModal } from '../components/ReceiptFormModal';
import { LABELS, RECEIPT_STATUS_LABEL } from '../constants';
import { useReceipts } from '../hooks/useReceipts';
import type { MaterialReceipt, MaterialReceiptListItem } from '../types';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';

type Filters = { keyword?: string; status?: string };

const STATUS_OPTIONS = Object.entries(RECEIPT_STATUS_LABEL).map(([value, l]) => ({
  value,
  label: l.text,
}));

/** UC37 / UC39: danh sách phiếu nhập NVL (Thủ kho lập, Quản đốc xác nhận). */
export function MaterialReceiptListPage() {
  const table = useTableQuery<Filters>({});
  const receipts = useReceipts(table.params);
  const [viewingId, setViewingId] = useState<number>();
  const [editing, setEditing] = useState<MaterialReceipt | null>(null);
  const [formOpen, setFormOpen] = useState(false);

  const openForm = (receipt: MaterialReceipt | null) => {
    setViewingId(undefined);
    setEditing(receipt);
    setFormOpen(true);
  };

  const columns: TableColumnsType<MaterialReceiptListItem> = [
    {
      title: LABELS.receiptCode,
      dataIndex: 'receiptCode',
      render: (v: string, r) => <a onClick={() => setViewingId(r.materialReceiptId)}>{v}</a>,
    },
    { title: LABELS.order, dataIndex: 'orderCode' },
    { title: LABELS.receiptDate, dataIndex: 'receiptDate', render: formatDate },
    { title: LABELS.lineCount, dataIndex: 'lineCount', align: 'right' },
    { title: LABELS.submittedAt, dataIndex: 'submittedAt', render: formatDateTime },
    {
      title: LABELS.status,
      dataIndex: 'status',
      render: (v: string) => <StatusTag status={v} labels={RECEIPT_STATUS_LABEL} />,
    },
    {
      title: LABELS.confirmedBy,
      dataIndex: 'confirmedByName',
      render: (v: string | null) => v ?? '—',
    },
  ];

  return (
    <Card
      title={LABELS.receiptsTitle}
      extra={
        <Can roles={[ROLES.WarehouseKeeper]}>
          <Button type="primary" onClick={() => openForm(null)}>
            {MESSAGES.common.create}
          </Button>
        </Can>
      }
    >
      <Space style={{ marginBottom: 16 }} wrap>
        <DebouncedSearch
          allowClear
          placeholder={MESSAGES.common.search}
          onSearch={(keyword) => table.setFilters({ keyword: keyword || undefined })}
          style={{ width: 280 }}
        />
        <FilterSelect value={table.filters.status}
          placeholder={LABELS.status}
          options={STATUS_OPTIONS}
          onChange={(status?: string) => table.setFilters({ status })}
          style={{ width: 180 }}
        />
      </Space>
      <QueryError error={receipts.error} onRetry={() => void receipts.refetch()} />
      <Table
        rowKey="materialReceiptId"
        columns={columns}
        dataSource={receipts.data?.items}
        loading={receipts.isFetching}
        pagination={table.pagination(receipts.data?.totalItems)}
        locale={{ emptyText: MESSAGES.common.empty }}
      />
      <ReceiptDetailDrawer
        receiptId={viewingId}
        onClose={() => setViewingId(undefined)}
        onEdit={openForm}
      />
      <ReceiptFormModal open={formOpen} receipt={editing} onClose={() => setFormOpen(false)} />
    </Card>
  );
}
