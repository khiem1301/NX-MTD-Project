import { useEffect } from 'react';
import { Button, Form, Input, InputNumber, Modal, Space } from 'antd';
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import { MESSAGES } from '@/constants/messages';
import { LABELS } from '../constants';
import { useCreatePurchaseOrder } from '../hooks/usePurchaseOrders';
import type { CreatePurchaseOrderPayload } from '../types';
import { isNewMaterial, newMaterialName } from '../newMaterial';
import { MaterialFreeSelect } from './MaterialFreeSelect';

type PurchaseOrderFormModalProps = {
  open: boolean;
  onClose: () => void;
};

type LineValues = {
  material?: number | string;
  unit?: string;
  materialColor?: string;
  orderedQty: number;
  unitPrice: number;
};
type FormValues = { note?: string; lines: LineValues[] };

const EMPTY_LINE: LineValues = { orderedQty: 1, unitPrice: 0 };

const toPayload = (values: FormValues): CreatePurchaseOrderPayload => ({
  note: values.note,
  lines: values.lines.map(({ material, unit, ...rest }) =>
    isNewMaterial(material)
      ? { ...rest, newMaterialName: newMaterialName(material), newMaterialUnit: unit }
      : { ...rest, materialId: material },
  ),
});

/**
 * Quản đốc tạo đơn mua NVL dùng chung (kim, chỉ... cho nhiều đơn), không gắn mã đơn hàng
 * nên không cần Admin tạo đơn trước. NVL chưa có trong danh mục thì gõ tên + đơn vị để thêm mới.
 */
export function PurchaseOrderFormModal({ open, onClose }: PurchaseOrderFormModalProps) {
  const [form] = Form.useForm<FormValues>();
  const create = useCreatePurchaseOrder();

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    form.setFieldsValue({ lines: [EMPTY_LINE] });
  }, [open, form]);

  const required = [{ required: true, message: MESSAGES.common.required }];

  return (
    <Modal
      open={open}
      width={980}
      title={LABELS.stockPurchase}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={create.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form
        form={form}
        layout="vertical"
        onFinish={(values) => create.mutate(toPayload(values), { onSuccess: onClose })}
        disabled={create.isPending}
      >
        <Form.List name="lines">
          {(fields, { add, remove }) => (
            <>
              {fields.map((field) => (
                <Space key={field.key} align="start" wrap>
                  <Form.Item name={[field.name, 'material']} rules={required}>
                    <MaterialFreeSelect />
                  </Form.Item>
                  <Form.Item noStyle shouldUpdate>
                    {({ getFieldValue }) =>
                      isNewMaterial(getFieldValue(['lines', field.name, 'material'])) && (
                        <Form.Item name={[field.name, 'unit']} rules={required}>
                          <Input placeholder={LABELS.unit} maxLength={20} style={{ width: 110 }} />
                        </Form.Item>
                      )
                    }
                  </Form.Item>
                  <Form.Item name={[field.name, 'materialColor']}>
                    <Input placeholder={LABELS.color} maxLength={50} style={{ width: 130 }} />
                  </Form.Item>
                  <Form.Item name={[field.name, 'orderedQty']} rules={required}>
                    <InputNumber
                      addonBefore={LABELS.orderedQty}
                      min={0.001}
                      style={{ width: 170 }}
                    />
                  </Form.Item>
                  <Form.Item name={[field.name, 'unitPrice']} rules={required}>
                    <InputNumber addonBefore={LABELS.unitPrice} min={0} style={{ width: 190 }} />
                  </Form.Item>
                  <Button
                    icon={<DeleteOutlined />}
                    onClick={() => remove(field.name)}
                    disabled={fields.length === 1}
                  />
                </Space>
              ))}
              <Button type="dashed" icon={<PlusOutlined />} onClick={() => add(EMPTY_LINE)}>
                {LABELS.addLine}
              </Button>
            </>
          )}
        </Form.List>
        <Form.Item name="note" label={LABELS.note} style={{ marginTop: 16 }}>
          <Input.TextArea rows={2} maxLength={1000} />
        </Form.Item>
      </Form>
    </Modal>
  );
}
