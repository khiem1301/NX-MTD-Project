import { useMemo, useState } from 'react';
import { EditOutlined } from '@ant-design/icons';
import { Input, Segmented, Space, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DataTable } from '@/components/common/DataTable';
import { IconAction } from '@/components/common/IconAction';
import { MESSAGES } from '@/constants/messages';
import { foldVietnamese } from '@/utils/colorSearch';
import { fill } from '@/utils/format';
import { ASSIGNMENT_SEARCH_MAX_LENGTH, PRODUCTION_TEXT } from '../constants';
import type { AssignmentRow } from '../utils/assignmentBoard';
import { WorkerOperationsModal } from './WorkerOperationsModal';

const text = PRODUCTION_TEXT.assignments;
const t = text.byWorker;
const ALL = 'all';
const IDLE = 'idle';
/** Số mã công đoạn hiện trực tiếp trên dòng, phần còn lại gộp thành "+n". */
const OPERATION_TAGS_SHOWN = 4;

type AssignmentByWorkerProps = {
  orderId: number;
  /** Các công đoạn của mã hàng đang làm. */
  rows: AssignmentRow[];
  workerOptions: { value: number; label: string }[];
};

type WorkerRow = {
  workerId: number;
  label: string;
  operations: AssignmentRow['operation'][];
};

/**
 * Giao việc theo công nhân: bảng công nhân của tổ (số công đoạn và các công đoạn đang làm của mã hàng), lọc được
 * người chưa có việc. Nút thao tác mở khung chọn công đoạn cho người đó.
 */
export function AssignmentByWorker({ orderId, rows, workerOptions }: AssignmentByWorkerProps) {
  const [keyword, setKeyword] = useState('');
  const [filter, setFilter] = useState<typeof ALL | typeof IDLE>(ALL);
  const [editingId, setEditingId] = useState<number | null>(null);

  const all = useMemo<WorkerRow[]>(
    () =>
      workerOptions.map((o) => ({
        workerId: o.value,
        label: o.label,
        operations: rows
          .filter((r) => r.assignments.some((a) => a.workerId === o.value))
          .map((r) => r.operation),
      })),
    [workerOptions, rows],
  );
  const idleCount = all.filter((w) => w.operations.length === 0).length;
  const kw = foldVietnamese(keyword.trim());
  const data = all.filter(
    (w) =>
      (filter === ALL || w.operations.length === 0) && (!kw || foldVietnamese(w.label).includes(kw)),
  );
  const editing = workerOptions.find((o) => o.value === editingId) ?? null;

  const columns: ColumnsType<WorkerRow> = [
    {
      title: t.columns.worker,
      key: 'worker',
      render: (_, w) => <Typography.Text strong>{w.label}</Typography.Text>,
    },
    {
      title: t.columns.load,
      key: 'load',
      width: 120,
      align: 'center',
      sorter: (a, b) => a.operations.length - b.operations.length,
      render: (_, w) => (
        <Tag color={w.operations.length === 0 ? 'orange' : 'default'} style={{ marginInlineEnd: 0 }}>
          {w.operations.length}
        </Tag>
      ),
    },
    {
      title: t.columns.operations,
      key: 'operations',
      render: (_, w) =>
        w.operations.length === 0 ? (
          <Typography.Text type="secondary">{t.noOperationsYet}</Typography.Text>
        ) : (
          <Space size={[4, 4]} wrap>
            {w.operations.slice(0, OPERATION_TAGS_SHOWN).map((o) => (
              <Tag key={o.operationId} title={o.operationName} style={{ marginInlineEnd: 0 }}>
                {o.operationCode}
              </Tag>
            ))}
            {w.operations.length > OPERATION_TAGS_SHOWN && (
              <Typography.Text type="secondary">
                {fill(t.moreOperations, w.operations.length - OPERATION_TAGS_SHOWN)}
              </Typography.Text>
            )}
          </Space>
        ),
    },
    {
      title: MESSAGES.common.actions,
      key: 'actions',
      render: (_, w) => (
        <IconAction title={t.pick} icon={<EditOutlined />} onClick={() => setEditingId(w.workerId)} />
      ),
    },
  ];

  return (
    <>
      <Space wrap style={{ margin: '12px 0' }}>
        <Input.Search
          allowClear
          style={{ width: 240 }}
          placeholder={t.searchWorker}
          maxLength={ASSIGNMENT_SEARCH_MAX_LENGTH}
          value={keyword}
          onChange={(e) => setKeyword(e.target.value)}
        />
        <Segmented
          value={filter}
          onChange={(v) => setFilter(v as typeof ALL | typeof IDLE)}
          options={[
            { value: ALL, label: fill(t.workerAll, all.length) },
            { value: IDLE, label: fill(t.workerIdle, idleCount) },
          ]}
        />
      </Space>
      <DataTable<WorkerRow>
        rowKey="workerId"
        columns={columns}
        dataSource={data}
        locale={{ emptyText: t.noWorkerMatch }}
      />
      <WorkerOperationsModal
        open={editing !== null}
        orderId={orderId}
        worker={editing}
        rows={rows}
        onClose={() => setEditingId(null)}
      />
    </>
  );
}
