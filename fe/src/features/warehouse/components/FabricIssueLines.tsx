import { useEffect } from 'react';
import { Form, InputNumber, Space, Typography, type FormInstance } from 'antd';
import { formatQty } from '@/utils/format';
import { QtyInput } from '@/components/common/QtyInput';
import { MATERIAL_QTY_DECIMALS } from '@/constants/quantity';
import { LABELS } from '../constants';
import { useWorkOrderFabrics } from '../hooks/useIssues';
import { FabricSuggestionBar } from './FabricSuggestionBar';

type FabricIssueLinesProps = { form: FormInstance; workOrderId: number | undefined };

/** Dòng cấp vải: liệt kê vải của lệnh cắt (theo định mức mã hàng), Thủ kho nhập số cấp cho vải cần cấp (không vượt khả dụng). */
export function FabricIssueLines({ form, workOrderId }: FabricIssueLinesProps) {
  const fabrics = useWorkOrderFabrics(workOrderId);

  useEffect(() => {
    if (!fabrics.data) return;
    form.setFieldValue(
      'lines',
      fabrics.data.map((f) => ({ materialId: f.materialId, issuedQty: undefined })),
    );
  }, [fabrics.data, form]);

  const byKey = new Map((fabrics.data ?? []).map((f) => [f.materialId, f]));

  return (
    <>
      <FabricSuggestionBar form={form} workOrderId={workOrderId} fabrics={fabrics.data ?? []} />
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
                  {LABELS.issuedAlready}: {formatQty(fabric?.issuedQty)} · {LABELS.availableQty}:{' '}
                  {formatQty(fabric?.availableQty)} {fabric?.unit}
                </Typography.Text>
                <Form.Item name={[field.name, 'issuedQty']}>
                  <QtyInput
                    decimals={MATERIAL_QTY_DECIMALS}
                    addonBefore={LABELS.issueNow}
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
    </>
  );
}
