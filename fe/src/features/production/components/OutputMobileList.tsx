import { Card, InputNumber, Space, Typography } from 'antd';
import { formatCurrency, formatDateTime } from '@/utils/format';
import { OUTPUT_MAX_QTY, PRODUCTION_TEXT } from '../constants';
import type { OutputGridRow } from '../types';
import { outputKeyOf } from '../utils';
import { OutputRowTags } from './OutputRowTags';

const text = PRODUCTION_TEXT.output;

type OutputMobileListProps = {
  rows: OutputGridRow[];
  isManager: boolean;
  loading: boolean;
  edits: Record<string, number | null>;
  /** Chưa chọn dòng đơn hàng thì hiện lời nhắc thay cho "không có dòng". */
  noQuery: boolean;
  onQuantityChange: (row: OutputGridRow, value: number | null) => void;
};

/** Lưới sản lượng dạng thẻ cho điện thoại: ô nhập số lượng lớn, dễ bấm tại xưởng. */
export function OutputMobileList({
  rows,
  isManager,
  loading,
  edits,
  noQuery,
  onQuantityChange,
}: OutputMobileListProps) {
  return (
    <Space direction="vertical" size={8} style={{ width: '100%' }}>
      {rows.length === 0 && (
        <Typography.Text type="secondary">
          {noQuery ? PRODUCTION_TEXT.selectOrderLine : text.noRows}
        </Typography.Text>
      )}
      {rows.map((r) => (
        <Card key={outputKeyOf(r)} size="small" loading={loading}>
          <Space direction="vertical" size={6} style={{ width: '100%' }}>
            <Typography.Text strong>{`${r.operationCode} - ${r.operationName}`}</Typography.Text>
            <Typography.Text type="secondary">{`${r.workerCode} - ${r.workerName}`}</Typography.Text>
            <OutputRowTags row={r} />
            <InputNumber
              size="large"
              min={0}
              max={OUTPUT_MAX_QTY}
              precision={0}
              inputMode="numeric"
              addonBefore={text.columns.qty}
              style={{ width: '100%' }}
              disabled={!isManager && !r.canEdit}
              value={edits[outputKeyOf(r)] !== undefined ? edits[outputKeyOf(r)] : r.quantity}
              onChange={(v) => onQuantityChange(r, v)}
            />
            {r.unitRate !== null && (
              <Typography.Text type="secondary">{`${text.columns.rate}: ${formatCurrency(r.unitRate)}`}</Typography.Text>
            )}
            {r.editDeadline && (
              <Typography.Text type="secondary">{`${text.columns.deadline}: ${formatDateTime(r.editDeadline)}`}</Typography.Text>
            )}
          </Space>
        </Card>
      ))}
    </Space>
  );
}
