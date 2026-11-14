import { REASON_MAX_LENGTH } from '@/constants/limits';
import { useMemo, useState } from 'react';
import { HistoryOutlined, SearchOutlined } from '@ant-design/icons';
import { Button, Input, Modal, Select, Space, Tooltip, Typography } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { MESSAGES } from '@/constants/messages';
import { groupRowKey, useGroupAccordion } from '@/hooks/useGroupAccordion';
import { fill } from '@/utils/format';
import { PM_LABELS } from '../constants';
import { useSaveDaily } from '../hooks/usePieceMovement';
import type { DailyAllocationRow, OperationProgress } from '../types';
import {
  extraRowFrom,
  fillGive,
  groupDailyRows,
  isOverGive,
  matchesOperation,
  previousEdits,
  rowKeyOf,
  type DailyWorker,
} from '../utils/dailyBoard';
import { dailyModalColumns, type DailyTableRow } from './dailyModalColumns';

const TABLE_SCROLL = { x: 560, y: 'calc(100vh - 520px)' };

type DailyWorkerModalProps = {
  worker: DailyWorker;
  workDate: string;
  canEdit: boolean;
  /** Công đoạn của các dòng đơn đang xem, để thêm công đoạn ngoài phân công. */
  operations: OperationProgress[];
  onClose: () => void;
};

/**
 * Giao việc trong ngày cho một công nhân: công đoạn đã phân công lấy sẵn, thêm được công đoạn ngoài phân công. Bảng chỉ mở
 * một nhóm mỗi lúc (bấm nhóm để mở/đóng), có ô tìm công đoạn khi nhóm có nhiều công đoạn. Nhập ở dòng nhóm thì
 * điền cùng số cho cả nhóm (dòng vượt số giao được thì báo lỗi, không cho lưu); "Giao như lần trước" điền lại số của lần giao gần nhất. Trần sản lượng cuối ca = tồn từ hôm trước + số
 * giao trong ngày; hạ số xuống phải ghi lý do.
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
  // Ô vượt số giao tối đa đã báo lỗi tại dòng; còn dòng vượt thì không cho lưu.
  const over = changed.filter((r) => isOverGive(r, valueOf(r)));
  const multiLine = new Set(rows.map((r) => r.orderLineId)).size > 1;

  const accordion = useGroupAccordion(groupDailyRows(rows), matchesOperation);
  const tableRows = accordion.sections.flatMap(
    ({ group: g, shownRows, isOpen }): DailyTableRow[] => [
      {
        kind: 'group',
        key: groupRowKey(g.key),
        groupKey: g.key,
        name: g.name,
        rows: g.rows,
        isOpen,
        changedCount: g.rows.filter((r) => changed.includes(r)).length,
        overCount: g.rows.filter((r) => over.includes(r)).length,
      },
      ...(isOpen
        ? shownRows.map((row): DailyTableRow => ({ kind: 'operation', key: rowKeyOf(row), row }))
        : []),
    ],
  );
  const addable = operations.filter(
    (o) => !rows.some((r) => r.orderLineId === o.orderLineId && r.operationId === o.operationId),
  );
  const hasPrevious = rows.some((r) => r.previousQty > 0);

  const fillGroup = (groupKey: string, groupRows: DailyAllocationRow[], value: number | null) => {
    setGroupQty((prev) => ({ ...prev, [groupKey]: value }));
    if (value === null) return;
    setEdits((prev) => {
      const next = { ...prev };
      for (const r of groupRows) next[rowKeyOf(r)] = fillGive(r, value);
      return next;
    });
  };

  const columns = dailyModalColumns({
    editable: canEdit,
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
        disabled:
          !canEdit || changed.length === 0 || over.length > 0 || (hasDecrease && !reason.trim()),
      }}
      confirmLoading={save.isPending}
      onOk={handleSave}
      onCancel={onClose}
    >
      <Typography.Paragraph type="secondary">{PM_LABELS.workerModalHint}</Typography.Paragraph>
      {canEdit && (
        <Space.Compact style={{ width: '100%', marginBottom: 12 }}>
          <Select<string>
            showSearch
            optionFilterProp="label"
            style={{ width: '100%' }}
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
          <Tooltip title={PM_LABELS.giveLikePreviousHint}>
            <Button
              icon={<HistoryOutlined />}
              disabled={!hasPrevious}
              onClick={() => setEdits((prev) => ({ ...prev, ...previousEdits(rows) }))}
            >
              {PM_LABELS.giveLikePrevious}
            </Button>
          </Tooltip>
        </Space.Compact>
      )}
      <Input
        allowClear
        prefix={<SearchOutlined />}
        style={{ marginBottom: 12 }}
        placeholder={PM_LABELS.searchOperation}
        value={accordion.keyword}
        onChange={(e) => accordion.setKeyword(e.target.value)}
      />
      <DataTable<DailyTableRow>
        rowKey="key"
        size="small"
        columns={columns}
        dataSource={tableRows}
        pagination={false}
        scroll={TABLE_SCROLL}
        locale={accordion.keyword.trim() ? { emptyText: PM_LABELS.noMatchOperation } : undefined}
        onRow={(r) => (r.kind === 'group' ? accordion.groupRowProps(r.groupKey, r.isOpen) : {})}
      />
      {hasDecrease && (
        <Input.TextArea
          rows={2}
          maxLength={REASON_MAX_LENGTH}
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
