import { useEffect, useState } from 'react';
import { PlusOutlined } from '@ant-design/icons';
import { Button, Col, Divider, Form, Input, InputNumber, Modal, Row, Select } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { useMaterialOptions } from '@/hooks/useLookups';
import { LABELS } from '../constants';
import {
  deliveryCountProblem,
  MAX_NOTE_LENGTH,
  noteProblem,
  orderMaterialOptions,
  qtyProblem,
} from '../utils/orderMaterialRules';
import { useSaveOrderMaterial } from '../hooks/useOrderMaterials';
import type { OrderMaterial, SaveOrderMaterialPayload } from '../types';
import { MaterialFormModal } from './MaterialFormModal';
import { MODAL_WIDTH } from '@/components/common/modalSize';

type OrderMaterialFormModalProps = {
  open: boolean;
  orderId: number;
  orderMaterial: OrderMaterial | null;
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
      expectedDeliveryCount:
        (values.customerQty ?? 0) > 0 ? (values.expectedDeliveryCount ?? null) : null,
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
        <Row gutter={16}>
          <Col xs={24} sm={8}>
            <Form.Item
              name="requiredQty"
              label={LABELS.requiredQty}
              dependencies={['customerQty']}
              rules={[
                ...required,
                {
                  validator: async (_rule, value?: number) => {
                    const problem = qtyProblem(LABELS.requiredQtyName, value);
                    if (problem) throw new Error(problem);
                    const customer = form.getFieldValue('customerQty') as number | undefined;
                    if ((value ?? 0) <= 0 && (customer ?? 0) <= 0)
                      throw new Error(LABELS.qtyRequiredOrCustomer);
                  },
                },
              ]}
              initialValue={0}
            >
              <InputNumber style={{ width: '100%' }} />
            </Form.Item>
          </Col>
          <Col xs={24} sm={8}>
            <Form.Item
              name="customerQty"
              label={LABELS.customerQty}
              rules={[
                ...required,
                {
                  validator: async (_rule, value?: number) => {
                    const problem = qtyProblem(LABELS.customerQtyName, value);
                    if (problem) throw new Error(problem);
                  },
                },
              ]}
              initialValue={0}
            >
              <InputNumber style={{ width: '100%' }} />
            </Form.Item>
          </Col>
          <Col xs={24} sm={8}>
            <Form.Item
              name="expectedDeliveryCount"
              label={LABELS.expectedDeliveryCount}
              rules={[
                { required: (customerQty ?? 0) > 0, message: MESSAGES.common.required },
                {
                  validator: async (_rule, value?: number) => {
                    const problem = deliveryCountProblem(value);
                    if (problem) throw new Error(problem);
                  },
                },
              ]}
            >
              <InputNumber disabled={(customerQty ?? 0) <= 0} style={{ width: '100%' }} />
            </Form.Item>
          </Col>
        </Row>
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
