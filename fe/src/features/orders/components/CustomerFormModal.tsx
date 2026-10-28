import { useEffect } from 'react';
import { Form, Input, Modal } from 'antd';
import { MESSAGES } from '@/constants/messages';
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
            Object.entries(error.errors ?? {}).map(([name, errors]) => ({ name: name as keyof SaveCustomerPayload, errors })),
          ),
      },
    );
  };

  return (
    <Modal
      open={open}
      title={customer ? `${MESSAGES.common.edit} ${customer.customerName}` : MESSAGES.orders.addCustomer}
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
          rules={[{ required: true, whitespace: true, message: MESSAGES.common.required }, { max: 200 }]}
        >
          <Input />
        </Form.Item>
        <Form.Item name="phone" label={MESSAGES.orders.phone} rules={[{ required: true, whitespace: true, message: MESSAGES.common.required }, { max: 15 }, { pattern: /^[0-9+]*$/, message: 'Chỉ gồm chữ số và dấu +' }]}>
          <Input />
        </Form.Item>
        <Form.Item name="email" label={MESSAGES.orders.email} rules={[{ required: true, whitespace: true, message: MESSAGES.common.required }, { max: 100 }, { type: 'email' }]}>
          <Input />
        </Form.Item>
        <Form.Item name="defaultAddress" label={MESSAGES.orders.address} rules={[{ required: true, whitespace: true, message: MESSAGES.common.required }, { max: 500 }]}>
          <Input.TextArea rows={2} />
        </Form.Item>
      </Form>
    </Modal>
  );
}
