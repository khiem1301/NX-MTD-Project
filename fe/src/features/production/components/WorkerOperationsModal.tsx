import { useState } from 'react';
import { Input, Modal, Select, Space, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DataTable } from '@/components/common/DataTable';
import { FilterSelect } from '@/components/common/FilterSelect';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import {
  ASSIGNMENT_MAX_OPERATIONS,
  ASSIGNMENT_SEARCH_MAX_LENGTH,
  PRODUCTION_TEXT,
} from '../constants';
import { useSaveWorkerOperations } from '../hooks/useAssignments';
import {
  ASSIGNMENT_STATE,
  assignmentGroups,
  filterAssignmentRows,
  type AssignmentRow,
  type AssignmentState,
} from '../utils/assignmentBoard';

const text = PRODUCTION_TEXT.assignments;
const t = text.byWorker;

type WorkerOperationsModalProps = {
  open: boolean;
  orderId: number;
  worker: { value: number; label: string } | null;
  /** Các công đoạn của mã hàng đang làm. */
  rows: AssignmentRow[];
  onClose: () => void;
};

/** Phần thân bảng cuộn trong khung để modal không dài quá màn hình; đầu bảng luôn thấy. */
const TABLE_BODY_HEIGHT = 'calc(100vh - 430px)';

const columns: ColumnsType<AssignmentRow> = [
  {
    title: text.columns.operation,
    key: 'op',
    ellipsis: true,
    render: (_, { operation: o }) => (
      <Typography.Text strong>{`${o.operationCode} - ${o.operationName}`}</Typography.Text>
    ),
  },
  {
    title: t.modalColumns.group,
    key: 'group',
    width: 120,
    ellipsis: true,
    render: (_, { operation: o }) => o.groupName ?? '—',
  },
  {
    title: t.modalColumns.skill,
    key: 'skill',
    width: 70,
    align: 'center',
    render: (_, { operation: o }) => o.skillLevel ?? '—',
  },
  {
    title: t.modalColumns.time,
    key: 'time',
    width: 120,
    align: 'center',
    render: (_, { operation: o }) => o.standardTimeSec ?? '—',
  },
  {
    title: t.modalColumns.workers,
    key: 'workers',
    width: 110,
    align: 'center',
    render: (_, row) => (
      <Tag
        color={row.assignments.length === 0 ? 'orange' : 'default'}
        style={{ marginInlineEnd: 0 }}
      >
        {row.assignments.length}
      </Tag>
    ),
  },
];

/** Khung chọn công đoạn của một công nhân: tích / bỏ tích tùy ý (lọc theo nhóm, tìm mã tên), chỉ ghi khi bấm Lưu. */
export function WorkerOperationsModal({
  open,
  orderId,
  worker,
  rows,
  onClose,
}: WorkerOperationsModalProps) {
  const save = useSaveWorkerOperations();
  const [keyword, setKeyword] = useState<string>();
  const [group, setGroup] = useState<string>();
  const [state, setState] = useState<AssignmentState>(ASSIGNMENT_STATE.ALL);
  const own = (row: AssignmentRow) => row.assignments.find((a) => a.workerId === worker?.value);
  const original = rows.filter((r) => own(r)).map((r) => r.operation.operationId);
  // null = chưa chạm vào: dùng danh sách đang có, nên mở lại luôn khớp dữ liệu mới nhất.
  const [edited, setEdited] = useState<number[] | null>(null);
  const checked = edited ?? original;

  const toAdd = checked.filter((id) => !original.includes(id));
  const toRemove = original.filter((id) => !checked.includes(id));
  const tooMany = toAdd.length > ASSIGNMENT_MAX_OPERATIONS;
  const changed = toAdd.length + toRemove.length > 0;
  const visible = filterAssignmentRows(rows, { keyword, group, state });
  const missing = rows.filter((r) => !r.isCovered).length;

  const onSave = () => {
    if (!worker) return;
    const closeAssignmentIds = rows
      .filter((r) => toRemove.includes(r.operation.operationId))
      .flatMap((r) => own(r)?.operationAssignmentId ?? []);
    save.mutate(
      { orderId, workerId: worker.value, addOperationIds: toAdd, closeAssignmentIds },
      { onSuccess: onClose },
    );
  };

  return (
    <Modal
      open={open}
      width={900}
      style={{ top: 24 }}
      title={worker ? fill(t.modalTitle, worker.label) : ''}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      okButtonProps={{ disabled: !changed || tooMany }}
      confirmLoading={save.isPending}
      onOk={onSave}
      onCancel={onClose}
      afterOpenChange={(isOpen) => {
        if (isOpen) return;
        setEdited(null);
        setKeyword(undefined);
        setGroup(undefined);
        setState(ASSIGNMENT_STATE.ALL);
      }}
    >
      <Space direction="vertical" style={{ width: '100%' }}>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t.modalHint}
        </Typography.Paragraph>
        <Space wrap>
          <Input.Search
            allowClear
            style={{ width: 220 }}
            placeholder={text.search}
            maxLength={ASSIGNMENT_SEARCH_MAX_LENGTH}
            value={keyword ?? ''}
            onChange={(e) => setKeyword(e.target.value || undefined)}
          />
          <FilterSelect
            showSearch
            optionFilterProp="label"
            style={{ width: 200 }}
            popupMatchSelectWidth={false}
            placeholder={text.filterGroup}
            options={assignmentGroups(rows).map((g) => ({ value: g, label: g }))}
            value={group}
            onChange={setGroup}
          />
          <Select<AssignmentState>
            style={{ width: 190 }}
            value={state}
            onChange={setState}
            options={[
              { value: ASSIGNMENT_STATE.ALL, label: fill(text.stateAll, rows.length) },
              { value: ASSIGNMENT_STATE.MISSING, label: fill(text.stateMissing, missing) },
              {
                value: ASSIGNMENT_STATE.COVERED,
                label: fill(text.stateCovered, rows.length - missing),
              },
            ]}
          />
          <Typography.Text type={tooMany ? 'danger' : 'secondary'}>
            {tooMany
              ? fill(t.tooManyAdded, ASSIGNMENT_MAX_OPERATIONS)
              : changed
                ? fill(t.changes, toAdd.length, toRemove.length)
                : t.noChanges}
          </Typography.Text>
        </Space>
        <DataTable<AssignmentRow>
          rowKey={(r) => r.operation.operationId}
          columns={columns}
          dataSource={visible}
          locale={{ emptyText: rows.length === 0 ? text.empty : text.noMatch }}
          tableLayout="fixed"
          scroll={{ y: TABLE_BODY_HEIGHT }}
          sticky
          rowSelection={{
            selectedRowKeys: checked,
            preserveSelectedRowKeys: true,
            onChange: (keys) => setEdited(keys as number[]),
          }}
        />
      </Space>
    </Modal>
  );
}
