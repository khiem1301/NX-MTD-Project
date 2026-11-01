import type { TableColumnsType } from 'antd';
import { formatQty } from '@/utils/format';
import { LABELS } from '../constants';
import type { MaterialReceiptLine } from '../types';

/** Cột bảng dòng NVL của phiếu nhập. */
export const receiptLineColumns: TableColumnsType<MaterialReceiptLine> = [
  { title: LABELS.materialCode, dataIndex: 'materialCode' },
  { title: LABELS.materialName, dataIndex: 'materialName' },
  { title: LABELS.color, dataIndex: 'materialColor', render: (v: string) => v || '—' },
  { title: LABELS.unit, dataIndex: 'unit' },
  {
    title: LABELS.toReplenishQty,
    dataIndex: 'toReplenishQty',
    align: 'right',
    render: (v: number | null) => (v === null ? '—' : formatQty(v)),
  },
  { title: LABELS.acceptedQty, dataIndex: 'acceptedQty', align: 'right', render: formatQty },
];
