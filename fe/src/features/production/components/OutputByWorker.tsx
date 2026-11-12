import { useMemo, useState } from 'react';
import { EditOutlined } from '@ant-design/icons';
import { Input, Select, Space, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DataTable } from '@/components/common/DataTable';
import { IconAction } from '@/components/common/IconAction';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { useTeamMemberLookup } from '@/hooks/useHrLookups';
import { useAuth } from '@/hooks/useAuth';
import { fill, formatQty } from '@/utils/format';
import { ASSIGNMENT_SEARCH_MAX_LENGTH, PRODUCTION_TEXT } from '../constants';
import type { OutputEntryState } from '../hooks/useOutputEntry';
import {
  buildOutputWorkers,
  filterOutputWorkers,
  isDone,
  isMissing,
  OUTPUT_WORKER_STATE,
  type OutputWorker,
  type OutputWorkerState,
} from '../utils/outputWorkerBoard';
import { WorkerOutputModal } from './WorkerOutputModal';

const text = PRODUCTION_TEXT.output;
const t = text.byWorker;

/**
 * Nhập sản lượng theo công nhân: bảng công nhân của tổ, kèm số công đoạn đã nhập trong ca. Nút thao tác mở khung nhập
 * sản lượng của người đó. Tổ trưởng / tổ phó dùng cho tổ mình; Quản đốc dùng cho tổ may đang chọn, phải ghi lý do sửa.
 */
export function OutputByWorker({ entry }: { entry: OutputEntryState }) {
  const { user } = useAuth();
  const { isManager, isAllDay, teamId, query, rows, grid, save, saveRows } = entry;
  // Quản đốc xem tổ may đang chọn; tổ trưởng / tổ phó xem tổ của mình.
  const members = useTeamMemberLookup(isManager ? teamId : (user?.teamId ?? null));
  const [keyword, setKeyword] = useState('');
  const [state, setState] = useState<OutputWorkerState>(OUTPUT_WORKER_STATE.ALL);
  const [editingId, setEditingId] = useState<number | null>(null);

  const workers = useMemo(
    () =>
      buildOutputWorkers(
        rows,
        (members.data ?? [])
          .filter((m) => m.role === 'Worker')
          .map((m) => ({ value: m.employeeId, label: `${m.employeeCode} - ${m.fullName}` })),
      ),
    [rows, members.data],
  );
  const visible = filterOutputWorkers(workers, keyword, state);
  // Cả ngày chỉ xem: không mở modal nhập dù editingId còn từ lần chọn ca trước.
  const editing =
    isAllDay ? null : (workers.find((w) => w.workerId === editingId) ?? null);

  const columns: ColumnsType<OutputWorker> = [
    {
      title: text.columns.worker,
      key: 'worker',
      render: (_, w) => <Typography.Text strong>{w.label}</Typography.Text>,
    },
    {
      title: t.columns.entered,
      key: 'entered',
      width: 170,
      align: 'center',
      render: (_, w) => (
        <Tag
          color={isDone(w) ? 'green' : isMissing(w) ? 'orange' : 'default'}
          style={{ marginInlineEnd: 0 }}
        >
          {fill(t.enteredValue, w.enteredCount, w.enterableCount)}
        </Tag>
      ),
    },
    {
      title: t.columns.total,
      key: 'total',
      width: 130,
      align: 'right',
      render: (_, w) => formatQty(w.totalQty),
    },
    {
      responsive: ['md'],
      title: text.columns.warnings,
      key: 'warnings',
      render: (_, w) =>
        w.noAttendance ? <Tag color="orange">{text.tags.noAttendance}</Tag> : null,
    },
    {
      title: MESSAGES.common.actions,
      key: 'actions',
      render: (_, w) =>
        isAllDay ? (
          <span>
            <IconAction title={text.chooseShiftToEnter} icon={<EditOutlined />} disabled />
          </span>
        ) : (
          <IconAction
            title={t.enter}
            icon={<EditOutlined />}
            onClick={() => setEditingId(w.workerId)}
          />
        ),
    },
  ];

  if (query === null)
    return <Typography.Text type="secondary">{PRODUCTION_TEXT.selectOrderLine}</Typography.Text>;

  return (
    <>
      <Space wrap>
        <Input.Search
          allowClear
          style={{ width: 240 }}
          placeholder={t.search}
          maxLength={ASSIGNMENT_SEARCH_MAX_LENGTH}
          value={keyword}
          onChange={(e) => setKeyword(e.target.value)}
        />
        <Select<OutputWorkerState>
          style={{ width: 200 }}
          aria-label={t.stateLabel}
          value={state}
          onChange={setState}
          options={[
            { value: OUTPUT_WORKER_STATE.ALL, label: fill(t.stateAll, workers.length) },
            {
              value: OUTPUT_WORKER_STATE.MISSING,
              label: fill(t.stateMissing, workers.filter(isMissing).length),
            },
            {
              value: OUTPUT_WORKER_STATE.DONE,
              label: fill(t.stateDone, workers.filter(isDone).length),
            },
          ]}
        />
      </Space>
      <QueryError error={members.error} onRetry={() => void members.refetch()} />
      <DataTable<OutputWorker>
        rowKey="workerId"
        columns={columns}
        dataSource={visible}
        loading={grid.isFetching || members.isLoading}
        locale={{ emptyText: t.noWorkers }}
      />
      {!isAllDay && editing && (
        <WorkerOutputModal
          key={editing.workerId}
          worker={editing}
          rows={editing.rows}
          saving={save.isPending}
          requireReason={isManager}
          onSave={(changed, transferExcess, reason) =>
            saveRows(changed, () => setEditingId(null), transferExcess, reason)
          }
          onClose={() => setEditingId(null)}
        />
      )}
    </>
  );
}
