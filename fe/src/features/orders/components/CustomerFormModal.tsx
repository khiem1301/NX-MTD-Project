import { useEffect } from 'react';
import { Form, Input, Modal } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { ERROR_CODES } from '@/constants/errorCodes';
import { normalizePhone } from '@/utils/phone';
import { PARTNER_LIMITS, partnerRules } from '@/utils/partnerRules';
import { useSaveCustomer } from '../hooks/useOrders';
import type { Customer, SaveCustomerPayload } from '../types';

type CustomerFormModalProps = {
  open: boolean;
  customer: Customer | null;
  onClose: () => void;
  onSaved?: (customerId: number) => void;
};

export function CustomerFormModal({ open, customer, onClose, onSaved }: CustomerFormModalProps) {
  const [form] = Form.useForm<SaveCustomerPayload>();
  const save = useSaveCustomer();

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    if (customer) {
      form.setFieldsValue({
        customerName: customer.customerName,
        phone: customer.phone ?? undefined,
        email: customer.email ?? undefined,
        defaultAddress: customer.defaultAddress ?? undefined,
      });
    }
  }, [open, customer, form]);

  const handleFinish = (values: SaveCustomerPayload) => {
    save.mutate(
      { id: customer?.customerId, body: values },
      {
        onSuccess: (id) => {
          onSaved?.(id as number);
          onClose();
        },
        onError: (error) =>
          form.setFields(
            error.errorCode === ERROR_CODES.CustomerNameDuplicate
              ? [{ name: 'customerName', errors: [error.message] }]
              : Object.entries(error.errors ?? {}).map(([name, errors]) => ({
                  name: name as keyof SaveCustomerPayload,
                  errors,
                })),
          ),
      },
    );
  };

  return (
    <Modal
      open={open}
      title={
        customer ? `${MESSAGES.common.edit} ${customer.customerName}` : MESSAGES.orders.addCustomer
      }
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={save.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={save.isPending}>
        <Form.Item
          name="customerName"
          label={MESSAGES.orders.customerName}
          rules={[partnerRules.required(MESSAGES.orders.customerName, PARTNER_LIMITS.name)]}
        >
          <Input />
        </Form.Item>
        <Form.Item
          name="phone"
          normalize={normalizePhone}
          label={MESSAGES.orders.phone}
          rules={[partnerRules.phone]}
        >
          <Input maxLength={12} />
        </Form.Item>
        <Form.Item name="email" label={MESSAGES.orders.email} rules={[partnerRules.email]}>
          <Input />
        </Form.Item>
        <Form.Item
          name="defaultAddress"
          label={MESSAGES.orders.address}
          rules={[partnerRules.required(MESSAGES.orders.address, PARTNER_LIMITS.address)]}
        >
          <Input.TextArea rows={2} />
        </Form.Item>
      </Form>
    </Modal>
  );
}
