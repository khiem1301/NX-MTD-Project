import { Form, InputNumber } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { MESSAGES } from '@/constants/messages';
import { DISPATCH_LABELS } from '../constants';
import type { DispatchNoteLine } from '../types';

/** Cột bảng dòng hàng của phiếu xuất giao; kế toán xác nhận giao thì nhập được số lượng khách nhận. */
export const dispatchLineColumns = (canConfirm: boolean): ColumnsType<DispatchNoteLine> => [
  {
    title: DISPATCH_LABELS.orderLine,
    key: 'line',
    render: (_, l) => `${l.productCode} - ${l.color} - ${l.sizeCode}`,
  },
  { title: DISPATCH_LABELS.issueQty, dataIndex: 'issueQty', align: 'right' },
  {
    title: DISPATCH_LABELS.receivedQty,
    key: 'received',
    align: 'right',
    render: (_, l) =>
      canConfirm ? (
        <Form.Item
          name={['received', String(l.dispatchNoteLineId)]}
          noStyle
          rules={[{ required: true, message: MESSAGES.common.required }]}
        >
          <InputNumber min={0} max={l.issueQty} precision={0} />
        </Form.Item>
      ) : (
        (l.receivedQty ?? '—')
      ),
  },
];
