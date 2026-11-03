import { useMemo, useState } from 'react';
import { Input, Modal, Select, Typography } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import { DAILY_REASON_MAX_LENGTH, PM_LABELS } from '../constants';
import { useSaveDaily } from '../hooks/usePieceMovement';
import type { DailyAllocationRow, OperationProgress } from '../types';
import {
  extraRowFrom,
  groupDailyRows,
  maxGiveOf,
  rowKeyOf,
  type DailyWorker,
} from '../utils/dailyBoard';
import { dailyModalColumns, type DailyTableRow } from './dailyModalColumns';

const TABLE_SCROLL = { x: 560, y: 'calc(100vh - 480px)' };
const GROUP_ROW_STYLE = { background: 'rgba(0, 0, 0, 0.03)' };

type DailyWorkerModalProps = {
  worker: DailyWorker;
  workDate: string;
  canEdit: boolean;
  /** Công đoạn của các dòng đơn đang xem, để thêm công đoạn ngoài phân công. */
  operations: OperationProgress[];
  onClose: () => void;
};

/**
 * Giao việc trong ngày cho một công nhân: công đoạn đã phân công lấy sẵn, thêm được công đoạn ngoài phân công. Nhập ở dòng nhóm thì
 * điền cùng số cho cả nhóm. Số của ngày là trần sản lượng cuối ca; hạ số xuống phải ghi lý do.
 */
export function DailyWorkerModal({
  worker,
  workDate,
  canEdit,
  operations,
  onClose,
}: DailyWorkerModalProps) {
  const [extras, setExtras] = useState<DailyAllocationRow[]>([]);
  const [edits, setEdits] = useState<Record<string, number>>({});
  const [groupQty, setGroupQty] = useState<Record<string, number | null>>({});
  const [reason, setReason] = useState('');
  const save = useSaveDaily();

  const rows = useMemo(() => [...worker.rows, ...extras], [worker.rows, extras]);
  const valueOf = (r: DailyAllocationRow) => edits[rowKeyOf(r)] ?? r.allocatedQty;
  const changed = rows.filter((r) => valueOf(r) !== r.allocatedQty);
  const hasDecrease = changed.some((r) => valueOf(r) < r.allocatedQty);
  const multiLine = new Set(rows.map((r) => r.orderLineId)).size > 1;

  const tableRows = groupDailyRows(rows).flatMap((g): DailyTableRow[] => [
    { kind: 'group', key: `g:${g.key}`, groupKey: g.key, name: g.name, rows: g.rows },
    ...g.rows.map((row): DailyTableRow => ({ kind: 'operation', key: rowKeyOf(row), row })),
  ]);
  const addable = operations.filter(
    (o) => !rows.some((r) => r.orderLineId === o.orderLineId && r.operationId === o.operationId),
  );

  const fillGroup = (groupKey: string, groupRows: DailyAllocationRow[], value: number | null) => {
    setGroupQty((prev) => ({ ...prev, [groupKey]: value }));
    if (value === null) return;
    setEdits((prev) => {
      const next = { ...prev };
      for (const r of groupRows)
        next[rowKeyOf(r)] = Math.min(Math.max(value, r.outputQty), maxGiveOf(r));
      return next;
    });
  };

  const columns = dailyModalColumns({
    multiLine,
    groupQty,
    valueOf,
    onGroupChange: fillGroup,
    onQuantityChange: (row, value) =>
      setEdits((prev) => ({ ...prev, [rowKeyOf(row)]: value ?? 0 })),
  });

  const handleSave = () =>
    save.mutate(
      {
        workDate,
        reason: hasDecrease ? reason.trim() : undefined,
        items: changed.map((r) => ({
          workerId: worker.workerId,
          orderLineId: r.orderLineId,
          operationId: r.operationId,
          quantity: valueOf(r),
        })),
      },
      { onSuccess: onClose },
    );

  return (
    <Modal
      open
      width={MODAL_WIDTH.lg}
      title={fill(PM_LABELS.workerModalTitle, worker.label)}
      okText={`${MESSAGES.common.save}${changed.length > 0 ? ` (${changed.length})` : ''}`}
      cancelText={MESSAGES.common.cancel}
      okButtonProps={{
        disabled: !canEdit || changed.length === 0 || (hasDecrease && !reason.trim()),
      }}
      confirmLoading={save.isPending}
      onOk={handleSave}
      onCancel={onClose}
    >
      <Typography.Paragraph type="secondary">{PM_LABELS.workerModalHint}</Typography.Paragraph>
      {canEdit && (
        <Select<string>
          showSearch
          optionFilterProp="label"
          style={{ width: '100%', marginBottom: 12 }}
          placeholder={PM_LABELS.addOperationPlaceholder}
          value={null}
          onChange={(key) => {
            const op = addable.find((o) => `${o.orderLineId}-${o.operationId}` === key);
            if (op) setExtras((prev) => [...prev, extraRowFrom(worker, op)]);
          }}
          options={addable.map((o) => ({
            value: `${o.orderLineId}-${o.operationId}`,
            label: `${o.color}-${o.sizeCode} · ${o.operationCode} - ${o.operationName}`,
          }))}
        />
      )}
      <DataTable<DailyTableRow>
        rowKey="key"
        size="small"
        columns={columns}
        dataSource={tableRows}
        pagination={false}
        scroll={TABLE_SCROLL}
        onRow={(r) => (r.kind === 'group' ? { style: GROUP_ROW_STYLE } : {})}
      />
      {hasDecrease && (
        <Input.TextArea
          rows={2}
          maxLength={DAILY_REASON_MAX_LENGTH}
          showCount
          style={{ marginTop: 12 }}
          placeholder={PM_LABELS.reasonLabel}
          value={reason}
          onChange={(e) => setReason(e.target.value)}
        />
      )}
    </Modal>
  );
}
