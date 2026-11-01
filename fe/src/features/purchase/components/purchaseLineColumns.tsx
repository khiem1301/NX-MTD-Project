import type { ColumnsType } from 'antd/es/table';
import { formatCurrency, formatQty } from '@/utils/format';
import { PURCHASE_TEXT } from '../constants';
import type { PurchaseOrderLine } from '../types';

const text = PURCHASE_TEXT.detail;

/** Cột bảng dòng NVL của đơn mua. */
export const purchaseLineColumns: ColumnsType<PurchaseOrderLine> = [
  {
    title: text.material,
    key: 'material',
    render: (_, l) => `${l.materialCode} - ${l.materialName}`,
  },
  { title: text.color, dataIndex: 'materialColor', render: (value: string) => value || '—' },
  {
    title: text.qty,
    dataIndex: 'orderedQty',
    align: 'right',
    render: (value: number, l) => `${formatQty(value)} ${l.unit}`,
  },
  {
    title: text.unitPrice,
    dataIndex: 'unitPrice',
    align: 'right',
    render: (v: number) => formatCurrency(v),
  },
  {
    title: text.amount,
    key: 'amount',
    align: 'right',
    render: (_, l) => formatCurrency(l.orderedQty * l.unitPrice),
  },
];
