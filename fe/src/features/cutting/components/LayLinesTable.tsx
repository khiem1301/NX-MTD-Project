import { Form, Input, InputNumber, Tag } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import { LABELS, LAY_MAX_SMALLINT, lineKey } from '../constants';
import type { CuttingLayLine, WorkOrderLine } from '../types';
import { cutBeforeOf, remainingToCut } from '../utils/layRemaining';
import { DataTable } from '@/components/common/DataTable';

type LayLinesTableProps = {
  lines: WorkOrderLine[];
  /** Các dòng của bàn đang sửa (bàn mới thì rỗng), để không tính số của chính bàn này vào "đã cắt trước". */
  editingLines?: CuttingLayLine[];
};

/**
 * Từng màu - size của lệnh cắt: kế hoạch, đã cắt ở các bàn khác, còn cần cắt sau bàn này (đổi ngay khi nhập), tỉ lệ
 * (0 = không cắt), số tính = số lớp × tỉ lệ, số thực tế nếu khác.
 */
export function LayLinesTable({ lines, editingLines }: LayLinesTableProps) {
  const form = Form.useFormInstance();
  const plyCount = (Form.useWatch('plyCount', form) as number | undefined) ?? 0;
  const ratios = (Form.useWatch('ratio', form) as Record<string, number | null> | undefined) ?? {};
  const actuals =
    (Form.useWatch('actual', form) as Record<string, number | null> | undefined) ?? {};
  const thisLayQty = (l: WorkOrderLine) =>
    actuals[lineKey(l)] ?? plyCount * (ratios[lineKey(l)] ?? 0);

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
          width: 90,
          render: (_, l) => cutBeforeOf(l, editingLines),
        },
        {
          title: LABELS.remainingToCut,
          key: 'remaining',
          align: 'right',
          width: 100,
          render: (_, l) => {
            const left = remainingToCut(l.plannedQty, cutBeforeOf(l, editingLines), thisLayQty(l));
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
          title: LABELS.ratioValue,
          key: 'ratio',
          width: 90,
          render: (_, l) => (
            <Form.Item name={['ratio', lineKey(l)]} noStyle>
              <InputNumber min={0} max={LAY_MAX_SMALLINT} precision={0} style={{ width: '100%' }} />
            </Form.Item>
          ),
        },
        {
          title: LABELS.calculatedQty,
          key: 'calc',
          align: 'right',
          width: 80,
          render: (_, l) => plyCount * (ratios[lineKey(l)] ?? 0),
        },
        {
          title: LABELS.actualQty,
          key: 'actual',
          width: 110,
          render: (_, l) => (
            <Form.Item name={['actual', lineKey(l)]} noStyle>
              <InputNumber
                min={0}
                precision={0}
                style={{ width: '100%' }}
                placeholder={String(plyCount * (ratios[lineKey(l)] ?? 0))}
              />
            </Form.Item>
          ),
        },
        {
          title: LABELS.adjustReason,
          key: 'reason',
          render: (_, l) => (
            <Form.Item
              name={['reason', lineKey(l)]}
              dependencies={[['actual', lineKey(l)], 'plyCount', ['ratio', lineKey(l)]]}
              style={{ marginBottom: 0 }}
              rules={[
                {
                  validator: async (_rule, value?: string) => {
                    const actual = form.getFieldValue(['actual', lineKey(l)]) as
                      number | null | undefined;
                    const calc = plyCount * (ratios[lineKey(l)] ?? 0);
                    if (
                      actual !== null &&
                      actual !== undefined &&
                      actual !== calc &&
                      !value?.trim()
                    )
                      throw new Error(MESSAGES.cutting.adjustReasonRequired);
                  },
                },
              ]}
            >
              <Input maxLength={500} />
            </Form.Item>
          ),
        },
      ]}
    />
  );
}
