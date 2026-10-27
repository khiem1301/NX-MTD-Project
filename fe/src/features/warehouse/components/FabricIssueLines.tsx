import { useEffect } from 'react';
import { Form, InputNumber, Space, Typography, type FormInstance } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { formatQty } from '@/utils/format';
import { LABELS } from '../constants';
import { useWorkOrderFabrics } from '../hooks/useIssues';

type FabricIssueLinesProps = { form: FormInstance; workOrderId: number | undefined };

/** Dòng cấp vải: điền sẵn số còn phải cấp theo định mức của lệnh cắt (không vượt khả dụng). */
export function FabricIssueLines({ form, workOrderId }: FabricIssueLinesProps) {
  const fabrics = useWorkOrderFabrics(workOrderId);

  useEffect(() => {
    if (!fabrics.data) return;
    form.setFieldValue(
      'lines',
      fabrics.data
        .filter((f) => f.remainingQty > 0)
        .map((f) => ({
          materialId: f.materialId,
          issuedQty: Math.min(f.remainingQty, f.availableQty),
        })),
    );
  }, [fabrics.data, form]);

  const byKey = new Map((fabrics.data ?? []).map((f) => [f.materialId, f]));

  return (
    <Form.List name="lines">
      {(fields) =>
        fields.map((field) => {
          const line = form.getFieldValue(['lines', field.name]) as { materialId: number };
          const fabric = byKey.get(line.materialId);
          return (
            <Space key={field.key} align="baseline" wrap>
              <Form.Item name={[field.name, 'materialId']} hidden>
                <InputNumber />
              </Form.Item>
              <Typography.Text strong style={{ width: 320, display: 'inline-block' }}>
                {fabric
                  ? `${fabric.materialCode} ${fabric.materialName}${fabric.materialColor ? ` (${fabric.materialColor})` : ''}`
                  : line.materialId}
              </Typography.Text>
              <Typography.Text type="secondary">
                {LABELS.remainingQty}: {formatQty(fabric?.remainingQty)} · {LABELS.availableQty}:{' '}
                {formatQty(fabric?.availableQty)} {fabric?.unit}
              </Typography.Text>
              <Form.Item
                name={[field.name, 'issuedQty']}
                rules={[{ required: true, message: MESSAGES.common.required }]}
              >
                <InputNumber
                  addonBefore={LABELS.issuedQty}
                  min={0.001}
                  max={fabric?.availableQty}
                  style={{ width: 200 }}
                />
              </Form.Item>
            </Space>
          );
        })
      }
    </Form.List>
  );
}
