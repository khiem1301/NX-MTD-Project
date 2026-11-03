import { useState } from 'react';
import { Alert, Modal, Space, Typography } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import { PRODUCTION_TEXT } from '../constants';
import type { OutputGridRow, OutputLineInput } from '../types';
import { canFill, groupOutputRows, remainingOf } from '../utils/outputWorkerBoard';
import { workerOutputColumns, type WorkerOutputTableRow } from './workerOutputColumns';

const text = PRODUCTION_TEXT.output;
const t = text.byWorker;
/** Phần thân bảng cuộn trong khung để modal không dài quá màn hình; màn hẹp thì cuộn ngang. */
const TABLE_SCROLL = { x: 620, y: 'calc(100vh - 400px)' };
const GROUP_ROW_STYLE = { background: 'rgba(0, 0, 0, 0.03)' };

type WorkerOutputModalProps = {
  worker: { workerId: number; label: string };
  /** Các công đoạn công nhân đã được giao việc ở dòng đơn này. */
  rows: OutputGridRow[];
  saving: boolean;
  onSave: (rows: OutputLineInput[]) => void;
  onClose: () => void;
};

/**
 * Nhập sản lượng của một công nhân trong ca: các công đoạn lấy từ màn Giao việc, chia theo nhóm.
 * Nhập ở dòng nhóm thì điền cùng số đó cho các công đoạn nhập được trong nhóm; sửa lại từng công đoạn sau đó vẫn được.
 */
export function WorkerOutputModal({
  worker,
  rows,
  saving,
  onSave,
  onClose,
}: WorkerOutputModalProps) {
  const [edits, setEdits] = useState<Record<number, number | null>>({});
  const [groupQty, setGroupQty] = useState<Record<string, number | null>>({});

  // Ô để trống không đổi gì; số 0 ở dòng đã nhập nghĩa là xóa dòng đó.
  const changed = rows.flatMap((row) => {
    const quantity = edits[row.operationId];
    return quantity !== undefined && quantity !== null && quantity !== (row.quantity ?? 0)
      ? [{ row, quantity }]
      : [];
  });
  const overRows = changed.filter((c) => c.quantity > remainingOf(c.row)).map((c) => c.row);

  const tableRows = groupOutputRows(rows).flatMap((g): WorkerOutputTableRow[] => [
    { kind: 'group', key: `g:${g.key}`, groupKey: g.key, name: g.name, rows: g.rows },
    ...g.rows.map((row): WorkerOutputTableRow => ({
      kind: 'operation',
      key: `o:${row.operationId}`,
      row,
    })),
  ]);

  const fillGroup = (groupKey: string, groupRows: OutputGridRow[], value: number | null) => {
    setGroupQty((prev) => ({ ...prev, [groupKey]: value }));
    if (value === null) return;
    setEdits((prev) => {
      const next = { ...prev };
      for (const r of groupRows.filter(canFill)) next[r.operationId] = value;
      return next;
    });
  };

  const columns = workerOutputColumns({
    groupQty,
    overRows,
    valueOf: (r) => (r.operationId in edits ? (edits[r.operationId] ?? null) : r.quantity),
    onGroupChange: fillGroup,
    onQuantityChange: (operationId, value) =>
      setEdits((prev) => ({ ...prev, [operationId]: value })),
  });

  const save = () =>
    onSave(
      changed.map(({ row, quantity }) => ({
        operationId: row.operationId,
        workerId: worker.workerId,
        quantity,
        rowVer: row.rowVer,
      })),
    );

  return (
    <Modal
      open
      width={MODAL_WIDTH.lg}
      style={{ top: 24 }}
      title={fill(t.modalTitle, worker.label)}
      okText={`${text.save}${changed.length > 0 ? ` (${changed.length})` : ''}`}
      cancelText={MESSAGES.common.cancel}
      okButtonProps={{ disabled: changed.length === 0 || overRows.length > 0 }}
      confirmLoading={saving}
      onOk={save}
      onCancel={onClose}
    >
      <Space direction="vertical" style={{ width: '100%' }}>
        <Typography.Text type="secondary">{t.modalHint}</Typography.Text>
        {overRows.length > 0 && (
          <Alert
            type="error"
            showIcon
            message={t.overTitle}
            description={overRows.map((r) => (
              <div key={r.operationId}>
                {r.capQty > 0
                  ? fill(t.overCap, r.operationCode, remainingOf(r))
                  : fill(t.overNotAllocated, r.operationCode)}
              </div>
            ))}
          />
        )}
        <DataTable<WorkerOutputTableRow>
          rowKey="key"
          columns={columns}
          dataSource={tableRows}
          pagination={false}
          tableLayout="fixed"
          scroll={TABLE_SCROLL}
          locale={{ emptyText: t.noOperations }}
          onRow={(r) => (r.kind === 'group' ? { style: GROUP_ROW_STYLE } : {})}
        />
      </Space>
    </Modal>
  );
}
