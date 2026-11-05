import { Form, Typography } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import type { OrderLineOption } from '@/types/lookup';
import { QtyInput } from '@/components/common/QtyInput';
import { LABELS } from '../constants';

/** Đơn nhiều màu – size thì bảng cuộn bên trong, nút Lưu vẫn nằm trong màn hình. */
const TABLE_BODY_HEIGHT = 'max(160px, calc(100vh - 620px))';

/** Bảng kế hoạch cắt cả bộ theo từng màu – size của mã hàng; giá trị nằm trong form ở trường planned. Chưa chọn mã hàng thì chỉ hiện gợi ý. */
export function PlannedLinesTable({ lines }: { lines: OrderLineOption[] }) {
  if (lines.length === 0) {
    return (
      <Typography.Text
        type="secondary"
        style={{ display: 'block', padding: '8px 0', textAlign: 'center' }}
      >
        {LABELS.plannedLinesHint}
      </Typography.Text>
    );
  }
  return (
    <DataTable<OrderLineOption>
      rowKey="orderLineId"
      size="small"
      pagination={false}
      dataSource={lines}
      scroll={{ y: TABLE_BODY_HEIGHT }}
      columns={[
        {
          title: LABELS.orderLine,
          key: 'line',
          render: (_, l) => `${l.color} - ${l.sizeCode}`,
        },
        { title: LABELS.orderedQty, dataIndex: 'orderedQty', align: 'right', width: 110 },
        {
          title: LABELS.plannedQty,
          key: 'planned',
          width: 150,
          render: (_, l) => (
            <Form.Item name={['planned', String(l.orderLineId)]} noStyle>
              <QtyInput size="small" min={0} max={l.orderedQty} style={{ width: '100%' }} />
            </Form.Item>
          ),
        },
      ]}
    />
  );
}
