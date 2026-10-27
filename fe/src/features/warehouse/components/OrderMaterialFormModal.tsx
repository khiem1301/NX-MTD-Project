import { useEffect, useState } from 'react';
import { PlusOutlined } from '@ant-design/icons';
import { Button, Divider, Form, Input, InputNumber, Modal, Select, Space } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { useMaterialOptions } from '@/hooks/useLookups';
import { LABELS } from '../constants';
import { useSaveOrderMaterial } from '../hooks/useOrderMaterials';
import type { OrderMaterial, SaveOrderMaterialPayload } from '../types';
import { MaterialFormModal } from './MaterialFormModal';

type OrderMaterialFormModalProps = {
  open: boolean;
  orderId: number;
  orderMaterial: OrderMaterial | null;
  onClose: () => void;
};

type FormValues = SaveOrderMaterialPayload;

export function OrderMaterialFormModal({
  open,
  orderId,
  orderMaterial,
  onClose,
}: OrderMaterialFormModalProps) {
  const [form] = Form.useForm<FormValues>();
  const save = useSaveOrderMaterial();
  const materials = useMaterialOptions();
  const customerQty = Form.useWatch('customerQty', form);
  const [materialOpen, setMaterialOpen] = useState(false);

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    if (!orderMaterial) return;
    form.setFieldsValue({
      ...orderMaterial,
      note: orderMaterial.note ?? undefined,
      expectedDeliveryCount: orderMaterial.expectedDeliveryCount ?? undefined,
    });
  }, [open, orderMaterial, form]);

  const handleFinish = (values: FormValues) => {
    const body: SaveOrderMaterialPayload = {
      ...values,
      orderId,
      // Khách không cấp thì không có số lần giao (CK_OM_Delivery).
      expectedDeliveryCount: (values.customerQty ?? 0) > 0 ? (values.expectedDeliveryCount ?? null) : null,
    };
    save.mutate({ id: orderMaterial?.orderMaterialId, body }, { onSuccess: onClose });
  };

  const materialOptions = (materials.data ?? []).map((m) => ({
    value: m.materialId,
    label: `${m.materialCode} - ${m.materialName}${m.color ? ` - ${m.color}` : ''} (${m.unit})`,
  }));
  if (orderMaterial && !materialOptions.some((o) => o.value === orderMaterial.materialId)) {
    materialOptions.push({
      value: orderMaterial.materialId,
      label: `${orderMaterial.materialCode} - ${orderMaterial.materialName}${orderMaterial.materialColor ? ` - ${orderMaterial.materialColor}` : ''}`,
    });
  }
  const required = [{ required: true, message: MESSAGES.common.required }];

  return (
    <Modal
      open={open}
      width={760}
      title={
        orderMaterial
          ? `${MESSAGES.common.edit} ${orderMaterial.materialCode}`
          : MESSAGES.common.create
      }
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={save.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={save.isPending}>
        <Space wrap align="start">
          <Form.Item name="materialId" label={LABELS.material} rules={required}>
            <Select
              showSearch
              optionFilterProp="label"
              options={materialOptions}
              loading={materials.isLoading}
              style={{ width: 340 }}
              dropdownRender={(menu) => (
                <>
                  {menu}
                  <Divider style={{ margin: '8px 0' }} />
                  <Button type="link" icon={<PlusOutlined />} onClick={() => setMaterialOpen(true)}>
                    {LABELS.addMaterial}
                  </Button>
                </>
              )}
            />
          </Form.Item>
          <Form.Item
            name="requiredQty"
            label={LABELS.requiredQty}
            dependencies={["customerQty"]}
            rules={[
              ...required,
              {
                validator: async (_rule, value?: number) => {
                  const customer = form.getFieldValue("customerQty") as number | undefined;
                  if ((value ?? 0) <= 0 && (customer ?? 0) <= 0) throw new Error(LABELS.qtyRequiredOrCustomer);
                },
              },
            ]}
            initialValue={0}
          >
            <InputNumber min={0} style={{ width: 130 }} />
          </Form.Item>
          <Form.Item
            name="customerQty"
            label={LABELS.customerQty}
            rules={required}
            initialValue={0}
          >
            <InputNumber min={0} style={{ width: 130 }} />
          </Form.Item>
          <Form.Item
            name="expectedDeliveryCount"
            label={LABELS.expectedDeliveryCount}
            rules={[{ required: (customerQty ?? 0) > 0, message: MESSAGES.common.required }]}
          >
            <InputNumber min={1} max={99} precision={0} disabled={(customerQty ?? 0) <= 0} style={{ width: 150 }} />
          </Form.Item>
        </Space>
        <Form.Item name="note" label={LABELS.note}>
          <Input.TextArea rows={2} maxLength={500} />
        </Form.Item>
      </Form>
      <MaterialFormModal
        open={materialOpen}
        material={null}
        onClose={() => setMaterialOpen(false)}
        onSaved={(m) => form.setFieldValue('materialId', m.materialId)}
      />
    </Modal>
  );
}
