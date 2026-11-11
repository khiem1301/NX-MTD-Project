import { Form, Tag } from 'antd';
import { fill } from '@/utils/format';
import { LABELS, LAY_MAX_LINE_QTY, lineKey } from '../constants';
import type { CuttingLayLine, WorkOrderLine } from '../types';
import { cutBeforeOf, remainingToCut } from '../utils/layRemaining';
import { DataTable } from '@/components/common/DataTable';
import { QtyInput } from '@/components/common/QtyInput';

type LayLinesTableProps = {
  lines: WorkOrderLine[];
  /** Các dòng của bàn đang sửa (bàn mới thì rỗng), để không tính số của chính bàn này vào "đã cắt trước". */
  editingLines?: CuttingLayLine[];
};

/**
 * Từng màu - size của lệnh cắt: kế hoạch, đã cắt ở các bàn khác, còn cần cắt sau bàn này (đổi ngay khi nhập) và
 * số lượng sau cắt của bàn (trống / 0 = không cắt dòng này) (DEV-ALL-90).
 */
export function LayLinesTable({ lines, editingLines }: LayLinesTableProps) {
  const form = Form.useFormInstance();
  const qty = (Form.useWatch('qty', form) as Record<string, number | null> | undefined) ?? {};

  return (
    <DataTable<WorkOrderLine>
      rowKey={lineKey}
      size="small"
      pagination={false}
      dataSource={lines}
      columns={[
        {
          title: LABELS.orderLine,
          key: 'line',
          render: (_, l) =>
            `${l.color} - ${l.sizeCode}${l.componentName ? ` · ${l.componentName}` : ''}`,
        },
        { title: LABELS.plannedQty, dataIndex: 'plannedQty', align: 'right', width: 90 },
        {
          title: LABELS.cutBefore,
          key: 'cutBefore',
          align: 'right',
          width: 100,
          render: (_, l) => cutBeforeOf(l, editingLines),
        },
        {
          title: LABELS.remainingToCut,
          key: 'remaining',
          align: 'right',
          width: 110,
          render: (_, l) => {
            const left = remainingToCut(
              l.plannedQty,
              cutBeforeOf(l, editingLines),
              qty[lineKey(l)] ?? 0,
            );
            return left < 0 ? (
              <Tag color="orange" style={{ marginInlineEnd: 0 }}>
                {fill(LABELS.overCut, -left)}
              </Tag>
            ) : (
              <strong>{left}</strong>
            );
          },
        },
        {
          title: LABELS.actualQty,
          key: 'qty',
          width: 150,
          render: (_, l) => (
            <Form.Item name={['qty', lineKey(l)]} noStyle>
              <QtyInput
                min={0}
                max={LAY_MAX_LINE_QTY}
                style={{ width: '100%' }}
                aria-label={`${LABELS.actualQty} ${l.color} - ${l.sizeCode}`}
              />
            </Form.Item>
          ),
        },
      ]}
    />
  );
}
