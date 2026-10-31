import { useState } from 'react';
import { Button, Space } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons';
import type { Dayjs } from 'dayjs';
import { QueryError } from '@/components/common/QueryError';
import { ReasonModal } from '@/components/common/ReasonModal';
import { MESSAGES } from '@/constants/messages';
import { formatDate, formatDateTime, formatQty, toApiDate } from '@/utils/format';
import { QcCreateModal } from '../components/QcBatchModals';
import { PRODUCTION_TEXT, shiftText } from '../constants';
import { useDeleteQcBatch, useQcBatches } from '../hooks/useProduction';
import type { QcBatch, QcBatchQuery, QcRole } from '../types';
import { PageCard } from '@/components/common/PageCard';
import { DataTable } from '@/components/common/DataTable';
import { AppDatePicker } from '@/components/common/DateInputs';
import { QcEditModal } from '../components/QcEditModal';

const text = PRODUCTION_TEXT.qc;

/**
 * UC57 (KI-06) / UC22 (KI-11) — các đợt kiểm hoàn thiện. QA/QC thêm đợt, sửa / xóa trong ca; sau hết ca chỉ Quản đốc
 * sửa hoặc xóa và phải ghi lý do.
 */
function QcBoard({ role }: { role: QcRole }) {
  const isManager = role === 'manager';
  const [query, setQuery] = useState<QcBatchQuery>({ page: 1, pageSize: 20 });
  const [createOpen, setCreateOpen] = useState(false);
  const [editing, setEditing] = useState<QcBatch | null>(null);
  const [deleting, setDeleting] = useState<QcBatch | null>(null);
  const batches = useQcBatches(role, query);
  const remove = useDeleteQcBatch(role);

  const columns: ColumnsType<QcBatch> = [
    { title: text.columns.date, dataIndex: 'workDate', render: (v: string) => formatDate(v) },
    { title: text.columns.shift, dataIndex: 'shiftCode', render: (v: string) => shiftText(v) },
    { title: text.columns.time, dataIndex: 'batchTime', render: (v: string) => v.slice(0, 5) },
    { title: text.columns.team, dataIndex: 'sewingTeamName' },
    { responsive: ['md'], title: text.columns.order, dataIndex: 'orderCode' },
    {
      title: text.columns.line,
      key: 'line',
      render: (_, b) => `${b.productCode} - ${b.color} - ${b.sizeCode}`,
    },
    {
      title: text.columns.qty,
      dataIndex: 'finishedQty',
      align: 'right',
      render: (v: number) => formatQty(v),
    },
    { responsive: ['md'], title: text.columns.inspector, dataIndex: 'inspectorName' },
    {
      responsive: ['md'],
      title: text.columns.deadline,
      dataIndex: 'editDeadline',
      render: (v: string) => formatDateTime(v),
    },
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, b) =>
        isManager || b.canEdit ? (
          <Space size={0} wrap>
            <Button type="link" icon={<EditOutlined />} onClick={() => setEditing(b)}>
              {text.edit}
            </Button>
            <Button type="link" icon={<DeleteOutlined />} danger onClick={() => setDeleting(b)}>
              {text.delete}
            </Button>
          </Space>
        ) : null,
    },
  ];

  return (
    <PageCard
      title={isManager ? text.managerTitle : text.title}
      description={isManager ? text.managerSubtitle : text.subtitle}
      extra={
        !isManager && (
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>
            {text.add}
          </Button>
        )
      }
    >
      <Space wrap>
        <AppDatePicker
          allowClear
          placeholder={text.filterDate}
          onChange={(v: Dayjs | null) =>
            setQuery({ ...query, page: 1, workDate: v ? toApiDate(v) : undefined })
          }
        />
      </Space>

      <QueryError error={batches.error} onRetry={() => void batches.refetch()} />

      <DataTable<QcBatch>
        rowKey="qcBatchId"
        size="small"
        columns={columns}
        dataSource={batches.data?.items}
        loading={batches.isFetching}
        scroll={{ x: 'max-content' }}
        pagination={{
          current: batches.data?.page ?? query.page,
          pageSize: batches.data?.pageSize ?? query.pageSize,
          total: batches.data?.totalItems ?? 0,
          showSizeChanger: true,
          pageSizeOptions: [10, 20, 50],
          showTotal: (total, [from, to]) => `${from}–${to} / ${total} dòng`,
          onChange: (page, pageSize) => setQuery({ ...query, page, pageSize }),
        }}
      />

      {!isManager && <QcCreateModal open={createOpen} onClose={() => setCreateOpen(false)} />}
      <QcEditModal role={role} batch={editing} onClose={() => setEditing(null)} />
      <ReasonModal
        open={deleting !== null}
        title={text.deleteTitle}
        loading={remove.isPending}
        onCancel={() => setDeleting(null)}
        onSubmit={(reason) =>
          deleting &&
          remove.mutate(
            { id: deleting.qcBatchId, body: { reason, rowVer: deleting.rowVer } },
            { onSuccess: () => setDeleting(null) },
          )
        }
      />
    </PageCard>
  );
}

export const QcBatchesPage = () => <QcBoard role="qa-qc" />;
export const ManagerQcBatchesPage = () => <QcBoard role="manager" />;
