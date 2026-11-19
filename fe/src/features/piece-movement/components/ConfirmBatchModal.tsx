import { useState } from 'react';
import { Alert, Button, Input, Modal, Space, Table, Typography, type TableColumnsType } from 'antd';
import { QtyInput } from '@/components/common/QtyInput';
import { REASON_MAX_LENGTH } from '@/constants/limits';
import { MESSAGES } from '@/constants/messages';
import { fill, formatDate } from '@/utils/format';
import { PM_LABELS } from '../constants';
import { useConfirmBatchByWorker } from '../hooks/usePieceMovement';
import type { WorkAllocation } from '../types';
import {
  applySharedReason,
  batchQty,
  buildBatchItems,
  needsReason,
  type BatchEdit,
  type BatchEdits,
} from '../utils/confirmBatch';

type Props = { rows: WorkAllocation[]; onClose: () => void; onDone: () => void };

/**
 * DEV-ALL-126: công nhân xác nhận nhiều phiếu một lần; phiếu lệch số phải có lý do.
 * Trang chỉ render khi mở, nên mỗi lần mở là state mới.
 */
export function ConfirmBatchModal({ rows, onClose, onDone }: Props) {
  const confirm = useConfirmBatchByWorker();
  const [edits, setEdits] = useState<BatchEdits>({});
  const [shared, setShared] = useState('');
  const [submitted, setSubmitted] = useState(false);

  const patch = (id: number, change: BatchEdit) =>
    setEdits((prev) => ({ ...prev, [id]: { ...prev[id], ...change } }));
  const missing = rows.filter((m) => needsReason(m, edits)).length;

  const handleOk = () => {
    setSubmitted(true);
    const items = buildBatchItems(rows, edits);
    if (items) confirm.mutate({ items }, { onSuccess: onDone });
  };

  const columns: TableColumnsType<WorkAllocation> = [
    { title: PM_LABELS.workDateShort, dataIndex: 'workDate', render: (v: string) => formatDate(v) },
    {
      title: PM_LABELS.orderLine,
      key: 'line',
      render: (_, m) => `${m.orderCode} · ${m.productCode} ${m.color}-${m.sizeCode}`,
    },
    { title: PM_LABELS.operation, dataIndex: 'operationName' },
    { title: PM_LABELS.quantity, dataIndex: 'quantity', align: 'right', width: 80 },
    {
      title: PM_LABELS.confirmedQty,
      key: 'confirmedQty',
      width: 130,
      render: (_, m) => (
        <QtyInput
          min={1}
          max={m.quantity}
          value={batchQty(m, edits)}
          status={submitted && !batchQty(m, edits) ? 'error' : undefined}
          onChange={(v) => patch(m.workAllocationId, { confirmedQty: v ?? null })}
          aria-label={`${PM_LABELS.confirmedQty} ${m.operationName}`}
        />
      ),
    },
    {
      title: PM_LABELS.discrepancyReason,
      key: 'reason',
      render: (_, m) =>
        batchQty(m, edits) === m.quantity ? (
          <Typography.Text type="secondary">—</Typography.Text>
        ) : (
          <Input
            maxLength={REASON_MAX_LENGTH}
            value={edits[m.workAllocationId]?.discrepancyReason}
            status={needsReason(m, edits) ? 'error' : undefined}
            onChange={(e) => patch(m.workAllocationId, { discrepancyReason: e.target.value })}
            aria-label={`${PM_LABELS.discrepancyReason} ${m.operationName}`}
          />
        ),
    },
  ];

  return (
    <Modal
      open
      width={960}
      title={fill(PM_LABELS.batchTitle, rows.length)}
      okText={MESSAGES.common.confirm}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={confirm.isPending}
      onOk={handleOk}
      onCancel={onClose}
      destroyOnHidden
    >
      <Alert type="info" showIcon message={PM_LABELS.batchHint} style={{ marginBottom: 12 }} />
      <Space.Compact style={{ width: '100%', marginBottom: 12 }}>
        <Input
          addonBefore={PM_LABELS.sharedReason}
          placeholder={PM_LABELS.sharedReasonPlaceholder}
          maxLength={REASON_MAX_LENGTH}
          value={shared}
          onChange={(e) => setShared(e.target.value)}
        />
        <Button
          disabled={!shared.trim() || missing === 0}
          onClick={() => setEdits((prev) => applySharedReason(rows, prev, shared))}
        >
          {PM_LABELS.applySharedReason}
        </Button>
      </Space.Compact>
      <Table
        rowKey="workAllocationId"
        size="small"
        columns={columns}
        dataSource={rows}
        pagination={false}
        scroll={{ x: 'max-content', y: 420 }}
      />
      {missing > 0 && (
        <Alert
          type="error"
          showIcon
          message={fill(PM_LABELS.missingReasons, missing)}
          style={{ marginTop: 12 }}
        />
      )}
    </Modal>
  );
}
