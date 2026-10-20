import { Button, Form, Input, InputNumber, Select, Space } from 'antd';
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import { MESSAGES } from '@/constants/messages';
import type { OrderLineOption } from '@/types/lookup';
import { LABELS } from '../constants';

type NormFieldsProps = { orderLines: OrderLineOption[] };

const unique = (values: string[]) => [...new Set(values)].map((v) => ({ value: v, label: v }));
const MAX_WASTAGE = 100;

/** Danh sách dòng định mức: mã hàng × chi tiết × (màu, size tùy chọn) → số NVL / SP + % hao hụt. */
export function NormFields({ orderLines }: NormFieldsProps) {
  const products = [...new Map(orderLines.map((l) => [l.productId, l])).values()].map((l) => ({
    value: l.productId,
    label: l.productCode,
  }));
  const colors = unique(orderLines.map((l) => l.color));
  const sizes = unique(orderLines.map((l) => l.sizeCode));
  const required = [{ required: true, message: MESSAGES.common.required }];

  return (
    <Form.List name="norms">
      {(fields, { add, remove }) => (
        <>
          {fields.map((field) => (
            <Space key={field.key} align="start" wrap>
              <Form.Item name={[field.name, 'productId']} rules={required}>
                <Select placeholder={LABELS.product} options={products} style={{ width: 130 }} />
              </Form.Item>
              <Form.Item name={[field.name, 'componentName']}>
                <Input placeholder={LABELS.component} maxLength={100} style={{ width: 120 }} />
              </Form.Item>
              <Form.Item name={[field.name, 'garmentColor']}>
                <Select
                  allowClear
                  placeholder={LABELS.garmentColor}
                  options={colors}
                  style={{ width: 170 }}
                />
              </Form.Item>
              <Form.Item name={[field.name, 'sizeCode']}>
                <Select
                  allowClear
                  placeholder={LABELS.sizeCode}
                  options={sizes}
                  style={{ width: 150 }}
                />
              </Form.Item>
              <Form.Item name={[field.name, 'qtyPerPiece']} rules={required}>
                <InputNumber
                  placeholder={LABELS.qtyPerPiece}
                  min={0.0001}
                  step={0.01}
                  style={{ width: 120 }}
                />
              </Form.Item>
              <Form.Item name={[field.name, 'wastagePercent']} initialValue={0}>
                <InputNumber addonAfter="%" min={0} max={MAX_WASTAGE} style={{ width: 110 }} />
              </Form.Item>
              <Button icon={<DeleteOutlined />} onClick={() => remove(field.name)} />
            </Space>
          ))}
          <Button
            type="dashed"
            icon={<PlusOutlined />}
            onClick={() => add()}
            disabled={products.length === 0}
          >
            {LABELS.addNorm}
          </Button>
        </>
      )}
    </Form.List>
  );
}
