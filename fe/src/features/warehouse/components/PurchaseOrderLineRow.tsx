import { Button, Form, Input, InputNumber, Space } from 'antd';
import { DeleteOutlined } from '@ant-design/icons';
import { MESSAGES } from '@/constants/messages';
import { LABELS } from '../constants';
import { isNewMaterial } from '../utils/newMaterial';
import { MaterialFreeSelect } from './MaterialFreeSelect';

type PurchaseOrderLineRowProps = {
  name: number;
  linked: boolean;
  canRemove: boolean;
  onRemove: () => void;
};

const required = [{ required: true, message: MESSAGES.common.required }];

/** Một dòng NVL của đơn mua; NVL chưa có trong danh mục thì nhập thêm đơn vị và màu. */
export function PurchaseOrderLineRow({
  name,
  linked,
  canRemove,
  onRemove,
}: PurchaseOrderLineRowProps) {
  return (
    <Space align="start" wrap style={{ display: 'flex' }}>
      <Form.Item name={[name, 'material']} rules={required}>
        <MaterialFreeSelect disabled={linked} />
      </Form.Item>
      <Form.Item noStyle shouldUpdate>
        {({ getFieldValue }) =>
          isNewMaterial(getFieldValue(['lines', name, 'material'])) && (
            <Space align="start">
              <Form.Item name={[name, 'unit']} rules={required}>
                <Input placeholder={LABELS.unit} maxLength={20} style={{ width: 110 }} />
              </Form.Item>
              <Form.Item name={[name, 'newMaterialColor']}>
                <Input placeholder={LABELS.color} maxLength={50} style={{ width: 130 }} />
              </Form.Item>
            </Space>
          )
        }
      </Form.Item>
      <Form.Item name={[name, 'orderedQty']} rules={required}>
        <InputNumber addonBefore={LABELS.orderedQty} min={0.001} style={{ width: 170 }} />
      </Form.Item>
      <Form.Item
        name={[name, 'unitPrice']}
        rules={[{ required: true, type: 'number', min: 0.01, message: LABELS.priceRequired }]}
      >
        <InputNumber addonBefore={LABELS.unitPrice} min={0} style={{ width: 190 }} />
      </Form.Item>
      <Button icon={<DeleteOutlined />} onClick={onRemove} disabled={!canRemove} />
    </Space>
  );
}
