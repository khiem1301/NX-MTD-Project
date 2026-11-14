import { useEffect } from 'react';
import { Form, Modal, Space, Typography } from 'antd';
import { QtyInput } from '@/components/common/QtyInput';
import type { Dayjs } from 'dayjs';
import { DeliveryDateFields } from '@/components/common/DeliveryDateFields';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { MESSAGES } from '@/constants/messages';
import { toDeliveryDateValues, toDeliveryDatesPayload } from '@/utils/expectedDelivery';
import { LABELS } from '../constants';
import { useSaveCustomerDelivery } from '../hooks/useOrderMaterials';
import type { OrderCustomerDelivery } from '../types';
import { MAX_DELIVERY_COUNT } from '../utils/orderMaterialRules';

type FormValues = {
  deliveryCount: number;
  expectedDeliveryDates?: (Dayjs | null)[];
};

type CustomerDeliveryPlanModalProps = {
  open: boolean;
  plan: OrderCustomerDelivery;
  onClose: () => void;
};

/** Khai / sửa lịch khách giao NVL của cả đơn: số lần và mỗi lần một ngày — DEV-ALL-95. */
export function CustomerDeliveryPlanModal({ open, plan, onClose }: CustomerDeliveryPlanModalProps) {
  const [form] = Form.useForm<FormValues>();
  const save = useSaveCustomerDelivery(plan.orderId);
  const count = Form.useWatch('deliveryCount', form);

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    form.setFieldsValue({
      deliveryCount: plan.deliveryCount ?? 1,
      expectedDeliveryDates: toDeliveryDateValues(
        plan.deliveryDates,
        plan.deliveryDate,
        plan.deliveryDateTo,
      ),
    });
  }, [open, plan, form]);

  const handleFinish = (values: FormValues) =>
    save.mutate(
      {
        deliveryCount: values.deliveryCount,
        deliveryDates: toDeliveryDatesPayload(values.deliveryCount, values.expectedDeliveryDates),
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
            <QtyInput min={1} max={MAX_DELIVERY_COUNT} style={{ width: 170 }} />
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
