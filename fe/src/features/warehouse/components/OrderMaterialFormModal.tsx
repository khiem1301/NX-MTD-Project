import { useEffect, useState } from 'react';
import { PlusOutlined } from '@ant-design/icons';
import { Button, Divider, Form, Input, Modal, Select } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { useMaterialOptions } from '@/hooks/useLookups';
import { LABELS } from '../constants';
import { MAX_NOTE_LENGTH, noteProblem, orderMaterialOptions } from '../utils/orderMaterialRules';
import { useSaveOrderMaterial } from '../hooks/useOrderMaterials';
import type { OrderMaterial, SaveOrderMaterialPayload } from '../types';
import { CustomerPlanNotice } from './CustomerPlanNotice';
import { MaterialFormModal } from './MaterialFormModal';
import { OrderMaterialQtyFields } from './OrderMaterialQtyFields';
import { MODAL_WIDTH } from '@/components/common/modalSize';

type OrderMaterialFormModalProps = {
  open: boolean;
  orderId: number;
  /** Dòng đang sửa (thêm mới dùng OrderMaterialExternalModal / OrderMaterialByProductModal). */
  orderMaterial: OrderMaterial;
  /** NVL đơn đã khai báo: không cho chọn lại vì mỗi NVL chỉ có một dòng trong đơn. */
  declared: OrderMaterial[];
  onClose: () => void;
};

type FormValues = SaveOrderMaterialPayload;

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
  const [materialOpen, setMaterialOpen] = useState(false);

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    form.setFieldsValue({
      materialId: orderMaterial.materialId,
      requiredQty: orderMaterial.requiredQty,
      customerQty: orderMaterial.customerQty,
      note: orderMaterial.note ?? undefined,
    });
  }, [open, orderMaterial, form]);

  // Lịch khách giao khai ở cả đơn (DEV-ALL-95) nên form chỉ gửi NVL, số lượng, ghi chú.
  const handleFinish = (values: FormValues) =>
    save.mutate(
      { id: orderMaterial.orderMaterialId, body: { ...values, orderId } },
      { onSuccess: onClose },
    );

  const materialOptions = orderMaterialOptions(materials.data ?? [], orderMaterial, declared);
  const required = [{ required: true, message: MESSAGES.common.required }];

  return (
    <Modal
      open={open}
      width={MODAL_WIDTH.md}
      title={`${MESSAGES.common.edit} ${orderMaterial.materialCode}`}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={save.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <CustomerPlanNotice orderId={orderId} />
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
        <OrderMaterialQtyFields />
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
