import { useEffect, useState } from 'react';
import { PlusOutlined } from '@ant-design/icons';
import { Button, Divider, Form, Input, Modal, Select } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { MESSAGES } from '@/constants/messages';
import { useMaterialOptions } from '@/hooks/useLookups';
import { LABELS } from '../constants';
import { MAX_NOTE_LENGTH, noteProblem, orderMaterialOptions } from '../utils/orderMaterialRules';
import { useSaveOrderMaterial } from '../hooks/useOrderMaterials';
import type { OrderMaterial, SaveOrderMaterialPayload } from '../types';
import { MaterialFormModal } from './MaterialFormModal';
import { OrderMaterialQtyFields } from './OrderMaterialQtyFields';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { toApiDate } from '@/utils/format';

type OrderMaterialFormModalProps = {
  open: boolean;
  orderId: number;
  orderMaterial: OrderMaterial | null;
  /** NVL đơn đã khai báo: không cho chọn lại vì mỗi NVL chỉ có một dòng trong đơn. */
  declared: OrderMaterial[];
  onClose: () => void;
};

type FormValues = Omit<SaveOrderMaterialPayload, 'expectedDeliveryDate'> & {
  expectedDeliveryDate?: Dayjs | null;
};

export function OrderMaterialFormModal({
  open,
  orderId,
  orderMaterial,
  declared,
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
      expectedDeliveryDate: orderMaterial.expectedDeliveryDate
        ? dayjs(orderMaterial.expectedDeliveryDate)
        : undefined,
    });
  }, [open, orderMaterial, form]);

  const handleFinish = (values: FormValues) => {
    const body: SaveOrderMaterialPayload = {
      ...values,
      orderId,
      // Khách không cấp thì không có số lần giao (CK_OM_Delivery).
      expectedDeliveryCount:
        (values.customerQty ?? 0) > 0 ? (values.expectedDeliveryCount ?? null) : null,
      expectedDeliveryDate:
        (values.customerQty ?? 0) > 0 && values.expectedDeliveryDate
          ? toApiDate(values.expectedDeliveryDate)
          : null,
    };
    save.mutate({ id: orderMaterial?.orderMaterialId, body }, { onSuccess: onClose });
  };

  const materialOptions = orderMaterialOptions(materials.data ?? [], orderMaterial, declared);
  const required = [{ required: true, message: MESSAGES.common.required }];

  return (
    <Modal
      open={open}
      width={MODAL_WIDTH.md}
      title={
        orderMaterial
          ? `${MESSAGES.common.edit} ${orderMaterial.materialCode}`
          : LABELS.addExternalMaterial
      }
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={save.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={save.isPending}>
        <Form.Item name="materialId" label={LABELS.material} rules={required}>
          <Select
            showSearch
            optionFilterProp="label"
            options={materialOptions}
            loading={materials.isLoading}
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
        <OrderMaterialQtyFields customerQty={customerQty} />
        <Form.Item
          name="note"
          label={LABELS.note}
          rules={[
            {
              validator: async (_rule, value?: string) => {
                const problem = noteProblem(value);
                if (problem) throw new Error(problem);
              },
            },
          ]}
        >
          <Input.TextArea
            rows={2}
            showCount={{ formatter: ({ count }) => `${count}/${MAX_NOTE_LENGTH}` }}
          />
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
