import type { ColumnsType } from 'antd/es/table';
import { DISPATCH_LABELS } from '../constants';
import type { DispatchNoteLine } from '../types';

/** Cột bảng dòng hàng của phiếu xuất giao; khách nhận đúng số xuất nên không có cột khách nhận (DEV-ALL-70). */
export const dispatchLineColumns: ColumnsType<DispatchNoteLine> = [
  {
    title: DISPATCH_LABELS.orderLine,
    key: 'line',
    render: (_, l) => `${l.productCode} - ${l.color} - ${l.sizeCode}`,
  },
  { title: DISPATCH_LABELS.issueQty, dataIndex: 'issueQty', align: 'right' },
];
