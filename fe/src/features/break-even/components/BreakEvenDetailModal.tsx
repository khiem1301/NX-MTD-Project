import type { ReactNode } from 'react';
import { Descriptions, Modal, Progress, Typography } from 'antd';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { fill, formatCurrency, formatDateTime, formatMonth, formatQty } from '@/utils/format';
import { BREAK_EVEN_TEXT } from '../constants';
import type { BreakEvenResultItem, BreakEvenRow } from '../types';

const T = BREAK_EVEN_TEXT.detail;
const PERCENT_CAP = 100;

/** Một ô của một cách chia; cách đó không có trong lần tính (lần tính cũ) thì báo thiếu. */
const cell = (item: BreakEvenResultItem | null, render: (i: BreakEvenResultItem) => ReactNode) =>
  item ? render(item) : <Typography.Text type="secondary">{T.noData}</Typography.Text>;

/** UC11 / SRS 6.2 — chi tiết hòa vốn một mã hàng: số chung, rồi so hai cách chia định phí cạnh nhau, kèm công thức. */
export function BreakEvenDetailModal({
  row,
  onClose,
}: {
  row: BreakEvenRow | null;
  onClose: () => void;
}) {
  if (!row) return null;
  const periodMonth = (row.output ?? row.labour)?.periodMonth ?? '';
  const perUnit = row.finishedQty > 0 ? row.variableCost / row.finishedQty : 0;
  const breakEven = (i: BreakEvenResultItem) =>
    i.breakEvenQty === null ? (
      <Typography.Text type="danger">{T.noBreakEven}</Typography.Text>
    ) : (
      <>
        <strong>{formatQty(i.breakEvenQty)}</strong>
        <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12 }}>
          {fill(
            T.breakEvenFormula,
            formatCurrency(i.allocatedFixedCost),
            formatCurrency(row.cmtUnitPrice),
            formatCurrency(perUnit),
          )}
        </Typography.Text>
      </>
    );
  const percent = (i: BreakEvenResultItem) =>
    i.percentReached === null ? (
      '—'
    ) : (
      <Progress
        percent={Math.min(PERCENT_CAP, i.percentReached)}
        format={() => `${i.percentReached}%`}
        status={i.percentReached >= PERCENT_CAP ? 'success' : 'exception'}
        size="small"
      />
    );
  const surplus = (i: BreakEvenResultItem) => (
    <Typography.Text strong type={i.surplusValue < 0 ? 'danger' : 'success'}>
      {formatCurrency(i.surplusValue)}
    </Typography.Text>
  );
  const both = (render: (i: BreakEvenResultItem) => ReactNode) => (
    <>
      <Descriptions.Item label={BREAK_EVEN_TEXT.basis.Output}>
        {cell(row.output, render)}
      </Descriptions.Item>
      <Descriptions.Item label={BREAK_EVEN_TEXT.basis.LabourHours}>
        {cell(row.labour, render)}
      </Descriptions.Item>
    </>
  );
  return (
    <Modal
      open
      width={MODAL_WIDTH.md}
      title={fill(T.title, row.productCode, formatMonth(periodMonth))}
      footer={null}
      onCancel={onClose}
      destroyOnHidden
    >
      <Typography.Text type="secondary">{formatDateTime(row.calculatedAt)}</Typography.Text>
      <Descriptions bordered size="small" column={1} style={{ margin: '8px 0 16px' }}>
        <Descriptions.Item label={T.revenue}>
          {fill(
            T.revenueFormula,
            formatQty(row.finishedQty),
            formatCurrency(row.cmtUnitPrice),
            formatCurrency(row.revenue),
          )}
          <Typography.Text type="secondary"> ({T.revenueSource})</Typography.Text>
        </Descriptions.Item>
        <Descriptions.Item label={T.variableCost}>
          {fill(T.variableFormula, formatCurrency(row.variableCost), formatCurrency(perUnit))}
          <Typography.Text type="secondary"> ({T.variableSource})</Typography.Text>
        </Descriptions.Item>
      </Descriptions>
      {[
        { label: T.share, render: (i: BreakEvenResultItem) => `${i.sharePct}%` },
        {
          label: T.allocated,
          render: (i: BreakEvenResultItem) => formatCurrency(i.allocatedFixedCost),
        },
        { label: T.breakEvenQty, render: breakEven },
        { label: T.percent, render: percent },
        { label: `${T.surplus} (${T.surplusFormula})`, render: surplus },
      ].map((line) => (
        <Descriptions
          key={line.label}
          title={line.label}
          bordered
          size="small"
          column={2}
          style={{ marginBottom: 8 }}
        >
          {both(line.render)}
        </Descriptions>
      ))}
    </Modal>
  );
}
