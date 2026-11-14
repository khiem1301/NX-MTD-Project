import { Form, Typography } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import type { OrderLineOption } from '@/types/lookup';
import { QtyInput } from '@/components/common/QtyInput';
import { maxQtyRule } from '@/utils/qtyLimit';
import { LABELS } from '../constants';

/** Đơn nhiều màu – size thì bảng cuộn bên trong, nút Lưu vẫn nằm trong màn hình. */
const TABLE_BODY_HEIGHT = 'max(160px, calc(100vh - 620px))';

type PlannedLinesTableProps = {
  lines: OrderLineOption[];
  /** Số kế hoạch cả bộ đã lưu của chính lệnh đang sửa theo dòng đơn (đã nằm trong plannedQty nên phải cộng trả lại). */
  ownPlanned?: Record<string, number>;
};

/**
 * Bảng kế hoạch cắt cả bộ theo từng màu – size của mã hàng; giá trị nằm trong form ở trường planned. Cột "Còn lên lệnh được" =
 * số đặt − kế hoạch của các lệnh cắt khác; nhập vượt thì cảnh báo nhưng vẫn lưu được. Chưa chọn mã hàng thì chỉ hiện gợi ý.
 */
export function PlannedLinesTable({ lines, ownPlanned = {} }: PlannedLinesTableProps) {
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
  const remainingOf = (l: OrderLineOption) =>
    Math.max(0, l.orderedQty - l.plannedQty + (ownPlanned[String(l.orderLineId)] ?? 0));
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
        { title: LABELS.orderedQty, dataIndex: 'orderedQty', align: 'right', width: 100 },
        {
          title: LABELS.plannableQty,
          key: 'plannable',
          align: 'right',
          width: 130,
          render: (_, l) => remainingOf(l),
        },
        {
          title: LABELS.plannedQty,
          key: 'planned',
          width: 150,
          render: (_, l) => (
            // Vượt số còn lên lệnh được chỉ cảnh báo, vẫn lưu được lệnh.
            <Form.Item
              name={['planned', String(l.orderLineId)]}
              rules={[
                maxQtyRule(remainingOf(l), { message: LABELS.overPlannableQty, warningOnly: true }),
              ]}
              style={{ marginBottom: 0 }}
            >
              <QtyInput size="small" min={0} style={{ width: '100%' }} />
            </Form.Item>
          ),
        },
      ]}
    />
  );
}
