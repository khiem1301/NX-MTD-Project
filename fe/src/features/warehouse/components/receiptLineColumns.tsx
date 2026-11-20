import { Tooltip, Typography, type TableColumnsType } from 'antd';
import { fill, formatQty } from '@/utils/format';
import { LABELS } from '../constants';
import type { MaterialReceiptLine } from '../types';

/** Cột bảng dòng NVL của phiếu nhập. */
export const receiptLineColumns: TableColumnsType<MaterialReceiptLine> = [
  { title: LABELS.materialCode, dataIndex: 'materialCode' },
  { title: LABELS.materialName, dataIndex: 'materialName' },
  { title: LABELS.color, dataIndex: 'materialColor', render: (v: string) => v || '—' },
  { title: LABELS.unit, dataIndex: 'unit' },
  {
    title: <Tooltip title={LABELS.deliveryNoHint}>{LABELS.deliveryNo}</Tooltip>,
    key: 'deliveryNo',
    align: 'center',
    render: (_, l) =>
      l.deliveryNo === null
        ? '—'
        : fill(LABELS.deliveryNoValue, l.deliveryNo, l.expectedDeliveryCount ?? 1),
  },
  { title: LABELS.acceptedQty, dataIndex: 'acceptedQty', align: 'right', render: formatQty },
  {
    title: <Tooltip title={LABELS.toReplenishQtyHint}>{LABELS.toReplenishQty}</Tooltip>,
    dataIndex: 'toReplenishQty',
    align: 'right',
    render: (v: number | null) =>
      v === null ? (
        '—'
      ) : v > 0 ? (
        <Typography.Text type="danger">{formatQty(v)}</Typography.Text>
      ) : (
        formatQty(v)
      ),
  },
];
