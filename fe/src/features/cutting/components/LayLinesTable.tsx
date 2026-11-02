import { Form, Input, InputNumber } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { LABELS, LAY_MAX_SMALLINT, lineKey } from '../constants';
import type { WorkOrderLine } from '../types';
import { DataTable } from '@/components/common/DataTable';

type LayLinesTableProps = { lines: WorkOrderLine[] };

/** Từng màu - size của lệnh cắt: tỉ lệ (0 = không cắt), số tính = số lớp × tỉ lệ, số thực tế nếu khác. */
export function LayLinesTable({ lines }: LayLinesTableProps) {
  const form = Form.useFormInstance();
  const plyCount = (Form.useWatch('plyCount', form) as number | undefined) ?? 0;
  const ratios = (Form.useWatch('ratio', form) as Record<string, number | null> | undefined) ?? {};

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
        { title: LABELS.cutQty, dataIndex: 'cutQty', align: 'right', width: 80 },
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
