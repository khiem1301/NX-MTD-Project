import { useEffect, useState } from 'react';
import { PlusOutlined } from '@ant-design/icons';
import { Button, DatePicker, Divider, Form, Input, InputNumber, Modal, Select, Space } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { MESSAGES } from '@/constants/messages';
import { useMaterialOptions, useOrderLineOptions } from '@/hooks/useLookups';
import { LABELS } from '../constants';
import { useSaveOrderMaterial } from '../hooks/useOrderMaterials';
import type { OrderMaterial, SaveNormPayload, SaveOrderMaterialPayload } from '../types';
import { MaterialFormModal } from './MaterialFormModal';
import { NormFields } from './NormFields';

type OrderMaterialFormModalProps = {
  open: boolean;
  orderId: number;
  orderMaterial: OrderMaterial | null;
  onClose: () => void;
};

type FormValues = Omit<SaveOrderMaterialPayload, 'customerExpectedDate'> & {
  customerExpectedDate?: Dayjs | null;
};

const DATE_FORMAT = 'DD/MM/YYYY';
const API_DATE = 'YYYY-MM-DD';

export function OrderMaterialFormModal({
  open,
  orderId,
  orderMaterial,
  onClose,
}: OrderMaterialFormModalProps) {
  const [form] = Form.useForm<FormValues>();
  const save = useSaveOrderMaterial();
  const materials = useMaterialOptions();
  const orderLines = useOrderLineOptions(orderId);
  const customerQty = Form.useWatch('customerQty', form);
  const [materialOpen, setMaterialOpen] = useState(false);

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    if (!orderMaterial) return;
    form.setFieldsValue({
      ...orderMaterial,
      note: orderMaterial.note ?? undefined,
      customerExpectedDate: orderMaterial.customerExpectedDate
        ? dayjs(orderMaterial.customerExpectedDate)
        : null,
      norms: orderMaterial.norms.map<SaveNormPayload>((n) => ({
        ...n,
        garmentColor: n.garmentColor ?? undefined,
        sizeCode: n.sizeCode ?? undefined,
      })),
    });
  }, [open, orderMaterial, form]);

  const handleFinish = (values: FormValues) => {
    const body: SaveOrderMaterialPayload = {
      ...values,
      orderId,
      norms: values.norms ?? [],
      customerExpectedDate: values.customerExpectedDate?.format(API_DATE) ?? null,
    };
    save.mutate({ id: orderMaterial?.orderMaterialId, body }, { onSuccess: onClose });
  };

  const materialOptions = (materials.data ?? []).map((m) => ({
    value: m.materialId,
    label: `${m.materialCode} - ${m.materialName} (${m.unit})`,
  }));
  if (orderMaterial && !materialOptions.some((o) => o.value === orderMaterial.materialId)) {
    materialOptions.push({
      value: orderMaterial.materialId,
      label: `${orderMaterial.materialCode} - ${orderMaterial.materialName}`,
    });
  }
  const required = [{ required: true, message: MESSAGES.common.required }];

  return (
    <Modal
      open={open}
      width={980}
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
          <Form.Item name="materialColor" label={LABELS.color}>
            <Input maxLength={50} style={{ width: 140 }} />
          </Form.Item>
          <Form.Item
            name="requiredQty"
            label={LABELS.requiredQty}
            rules={required}
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
            name="customerExpectedDate"
            label={LABELS.customerExpectedDate}
            rules={[{ required: (customerQty ?? 0) > 0, message: MESSAGES.common.required }]}
          >
            <DatePicker format={DATE_FORMAT} />
          </Form.Item>
        </Space>
        <Form.Item name="note" label={LABELS.note}>
          <Input.TextArea rows={2} maxLength={500} />
        </Form.Item>
        <Divider orientation="left">{LABELS.norms}</Divider>
        <NormFields orderLines={orderLines.data ?? []} />
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
