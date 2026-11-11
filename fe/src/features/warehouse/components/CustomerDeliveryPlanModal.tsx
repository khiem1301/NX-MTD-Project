import { useEffect } from 'react';
import { Form, InputNumber, Modal, Space, Typography } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { DeliveryDateFields } from '@/components/common/DeliveryDateFields';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { MESSAGES } from '@/constants/messages';
import { deliveryDateTo } from '@/utils/expectedDelivery';
import { toApiDate, toApiDateOrNull } from '@/utils/format';
import { LABELS } from '../constants';
import { useSaveCustomerDelivery } from '../hooks/useOrderMaterials';
import type { OrderCustomerDelivery } from '../types';
import { MAX_DELIVERY_COUNT } from '../utils/orderMaterialRules';

type FormValues = {
  deliveryCount: number;
  expectedDeliveryDate: Dayjs;
  expectedDeliveryDateTo?: Dayjs | null;
};

type CustomerDeliveryPlanModalProps = {
  open: boolean;
  plan: OrderCustomerDelivery;
  onClose: () => void;
};

const toDayjs = (value: string | null) => (value ? dayjs(value) : undefined);

/** Khai / sửa lịch khách giao NVL của cả đơn: số lần, ngày (giao từ 2 lần: khoảng ngày) — DEV-ALL-95. */
export function CustomerDeliveryPlanModal({ open, plan, onClose }: CustomerDeliveryPlanModalProps) {
  const [form] = Form.useForm<FormValues>();
  const save = useSaveCustomerDelivery(plan.orderId);
  const count = Form.useWatch('deliveryCount', form);

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    form.setFieldsValue({
      deliveryCount: plan.deliveryCount ?? 1,
      expectedDeliveryDate: toDayjs(plan.deliveryDate),
      expectedDeliveryDateTo: toDayjs(plan.deliveryDateTo),
    });
  }, [open, plan, form]);

  const handleFinish = (values: FormValues) =>
    save.mutate(
      {
        deliveryCount: values.deliveryCount,
        deliveryDate: toApiDate(values.expectedDeliveryDate),
        deliveryDateTo: toApiDateOrNull(
          deliveryDateTo(values.deliveryCount, values.expectedDeliveryDateTo),
        ),
      },
      { onSuccess: onClose },
    );

  return (
    <Modal
      open={open}
      width={MODAL_WIDTH.md}
      title={LABELS.customerDeliveryPlan}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={save.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Typography.Paragraph type="secondary">{LABELS.customerDeliveryHint}</Typography.Paragraph>
      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={save.isPending}>
        <Space wrap align="start">
          <Form.Item
            name="deliveryCount"
            label={LABELS.customerDeliveryCount}
            rules={[
              {
                required: true,
                type: 'number',
                min: Math.max(1, plan.receivedCount),
                message: LABELS.expectedDeliveryRequired,
              },
            ]}
          >
            <InputNumber min={1} max={MAX_DELIVERY_COUNT} precision={0} style={{ width: 170 }} />
          </Form.Item>
          <DeliveryDateFields
            count={count}
            label={LABELS.customerDeliveryDate}
            requiredMessage={LABELS.expectedDeliveryDateRequired}
          />
        </Space>
      </Form>
    </Modal>
  );
}
