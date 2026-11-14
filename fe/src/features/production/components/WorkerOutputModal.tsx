import { useState } from 'react';
import { SearchOutlined } from '@ant-design/icons';
import { Input, Modal, Space, Typography } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { MESSAGES } from '@/constants/messages';
import { groupRowKey, useGroupAccordion } from '@/hooks/useGroupAccordion';
import { confirmDialog } from '@/utils/confirmDialog';
import { fill } from '@/utils/format';
import { PRODUCTION_TEXT } from '../constants';
import type { OutputGridRow, OutputLineInput } from '../types';
import {
  canFill,
  canTransfer,
  excessOf,
  groupOutputRows,
  matchesOperation,
} from '../utils/outputWorkerBoard';
import { OutputReasonField } from './OutputReasonField';
import { WorkerOutputAlerts } from './WorkerOutputAlerts';
import { workerOutputColumns, type WorkerOutputTableRow } from './workerOutputColumns';

const text = PRODUCTION_TEXT.output;
const t = text.byWorker;
/** Phần thân bảng cuộn trong khung để modal không dài quá màn hình; màn hẹp thì cuộn ngang. */
const TABLE_SCROLL = { x: 620, y: 'calc(100vh - 440px)' };

type WorkerOutputModalProps = {
  worker: { workerId: number; label: string };
  /** Các công đoạn công nhân đã được giao việc ở dòng đơn này. */
  rows: OutputGridRow[];
  saving: boolean;
  /** `transferExcess` = có dòng vượt số được giao và tổ trưởng đã xác nhận chuyển phần vượt. */
  onSave: (
    rows: OutputLineInput[],
    transferExcess: boolean,
    reason?: string,
  ) => void | Promise<void>;
  onClose: () => void;
  /** Quản đốc sửa (kể cả sau hạn): bắt buộc ghi lý do sửa. */
  requireReason?: boolean;
};

/**
 * Nhập sản lượng của một công nhân trong ca: các công đoạn lấy từ màn Giao việc, chia theo nhóm. Bảng chỉ mở một nhóm
 * mỗi lúc (bấm nhóm để mở/đóng), có ô tìm công đoạn khi nhóm có nhiều công đoạn.
 * Nhập ở dòng nhóm thì điền cùng số đó cho các công đoạn nhập được trong nhóm; sửa lại từng công đoạn sau đó vẫn được.
 * Báo vượt số được giao mà người cùng công đoạn / phần tổ chưa giao còn đủ thì hỏi xác nhận rồi chuyển phần vượt khi lưu.
 */
export function WorkerOutputModal({
  worker,
  rows,
  saving,
  onSave,
  onClose,
  requireReason = false,
}: WorkerOutputModalProps) {
  const [edits, setEdits] = useState<Record<number, number | null>>({});
  const [reason, setReason] = useState('');
  const missingReason = requireReason && reason.trim() === '';
  const [groupQty, setGroupQty] = useState<Record<string, number | null>>({});

  // Ô để trống không đổi gì; số 0 ở dòng đã nhập nghĩa là xóa dòng đó.
  const changed = rows.flatMap((row) => {
    const quantity = edits[row.operationId];
    return quantity !== undefined && quantity !== null && quantity !== (row.quantity ?? 0)
      ? [{ row, quantity }]
      : [];
  });
  const over = changed.filter((c) => excessOf(c.row, c.quantity) > 0);
  const transferable = over.filter((c) => canTransfer(c.row, c.quantity));
  const overRows = over.filter((c) => !canTransfer(c.row, c.quantity)).map((c) => c.row);

  const accordion = useGroupAccordion(groupOutputRows(rows), matchesOperation);
  const tableRows = accordion.sections.flatMap(
    ({ group: g, shownRows, isOpen }): WorkerOutputTableRow[] => [
      {
        kind: 'group',
        key: groupRowKey(g.key),
        groupKey: g.key,
        name: g.name,
        rows: g.rows,
        isOpen,
        changedCount: changed.filter((c) => g.rows.includes(c.row)).length,
      },
      ...(isOpen
        ? shownRows.map((row): WorkerOutputTableRow => ({
            kind: 'operation',
            key: `o:${row.operationId}`,
            row,
          }))
        : []),
    ],
  );

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

  const save = () => {
    const toSave = changed.map(({ row, quantity }) => ({
      operationId: row.operationId,
      workerId: worker.workerId,
      quantity,
      rowVer: row.rowVer,
    }));
    // Chỉ Quản đốc gửi lý do sửa; tổ trưởng / tổ phó không có ô lý do.
    // Lưu xong thì đóng popup; lỗi API giữ popup để sửa lại (message lỗi đã hiện ở hook lưu).
    const send = async (transferExcess: boolean) => {
      try {
        await Promise.resolve(
          requireReason ? onSave(toSave, transferExcess, reason) : onSave(toSave, transferExcess),
        );
        onClose();
      } catch {
        /* giữ popup */
      }
    };
    if (transferable.length === 0) {
      void send(false);
      return;
    }
    confirmDialog({
      title: t.transferConfirmTitle,
      content: t.transferConfirmContent,
      onOk: () => send(true),
    });
  };

  return (
    <Modal
      open
      width={MODAL_WIDTH.lg}
      style={{ top: 24 }}
      title={fill(t.modalTitle, worker.label)}
      okText={`${text.save}${changed.length > 0 ? ` (${changed.length})` : ''}`}
      cancelText={MESSAGES.common.cancel}
      okButtonProps={{ disabled: changed.length === 0 || overRows.length > 0 || missingReason }}
      confirmLoading={saving}
      onOk={save}
      onCancel={onClose}
    >
      <Space direction="vertical" style={{ width: '100%' }}>
        <Typography.Text type="secondary">{t.modalHint}</Typography.Text>
        {requireReason && (
          <OutputReasonField
            value={reason}
            onChange={setReason}
            showError={missingReason && changed.length > 0}
          />
        )}
        <WorkerOutputAlerts overRows={overRows} transferable={transferable} />
        {rows.length > 0 && (
          <Input
            allowClear
            prefix={<SearchOutlined />}
            placeholder={t.searchOperation}
            value={accordion.keyword}
            onChange={(e) => accordion.setKeyword(e.target.value)}
          />
        )}
        <DataTable<WorkerOutputTableRow>
          rowKey="key"
          columns={columns}
          dataSource={tableRows}
          pagination={false}
          tableLayout="fixed"
          scroll={TABLE_SCROLL}
          locale={{
            emptyText: accordion.keyword.trim() ? t.noMatchOperation : t.noOperations,
          }}
          onRow={(r) => (r.kind === 'group' ? accordion.groupRowProps(r.groupKey, r.isOpen) : {})}
        />
      </Space>
    </Modal>
  );
}
