import { useEffect } from 'react';
import { Form, Input, Modal } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { ERROR_CODES } from '@/constants/errorCodes';
import { normalizePhone } from '@/utils/phone';
import { PARTNER_LIMITS, partnerRules } from '@/utils/partnerRules';
import { PURCHASE_TEXT } from '../constants';
import { useSaveSupplier } from '../hooks/usePurchase';
import type { SaveSupplierPayload, Supplier } from '../types';

type Props = { open: boolean; supplier: Supplier | null; onClose: () => void };

const blankToNull = (value: string | null | undefined): string | null => {
  const trimmed = value?.trim();
  return trimmed ? trimmed : null;
};

/** UC14: thêm / sửa nhà cung cấp (Admin). Tên không được trùng — BE kiểm và báo lỗi. */
export function SupplierFormModal({ open, supplier, onClose }: Props) {
  const [form] = Form.useForm<SaveSupplierPayload>();
  const save = useSaveSupplier(supplier?.supplierId ?? null);
  const text = PURCHASE_TEXT.supplierFields;

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    if (supplier) form.setFieldsValue(supplier);
  }, [open, supplier, form]);

  const handleFinish = (values: SaveSupplierPayload) => {
    save.mutate(
      {
        supplierName: values.supplierName.trim(),
        phone: blankToNull(values.phone),
        email: blankToNull(values.email),
        address: blankToNull(values.address),
        materialTypes: blankToNull(values.materialTypes),
        note: blankToNull(values.note),
      },
      {
        onSuccess: onClose,
        onError: (error) =>
          form.setFields(
            error.errorCode === ERROR_CODES.SupplierNameDuplicate
              ? [{ name: 'supplierName', errors: [error.message] }]
              : Object.entries(error.errors ?? {}).map(([name, errors]) => ({
                  name: name as keyof SaveSupplierPayload,
                  errors,
                })),
          ),
      },
    );
  };

  return (
    <Modal
      open={open}
      title={supplier ? PURCHASE_TEXT.editSupplier : PURCHASE_TEXT.add}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={save.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={save.isPending}>
        <Form.Item
          name="supplierName"
          label={text.name}
          rules={[partnerRules.required(text.name, PARTNER_LIMITS.name, text.nameRequired)]}
        >
          <Input maxLength={200} autoFocus />
        </Form.Item>
        <Form.Item
          name="phone"
          normalize={normalizePhone}
          label={text.phone}
          rules={[partnerRules.phone]}
        >
          <Input maxLength={12} />
        </Form.Item>
        <Form.Item name="email" label={text.email} rules={[partnerRules.email]}>
          <Input maxLength={100} />
        </Form.Item>
        <Form.Item
          name="address"
          label={text.address}
          rules={[partnerRules.required(text.address, PARTNER_LIMITS.address)]}
        >
          <Input maxLength={500} />
        </Form.Item>
        <Form.Item
          name="materialTypes"
          label={text.materialTypes}
          rules={[partnerRules.required(text.materialTypes, PARTNER_LIMITS.materialTypes)]}
        >
          <Input maxLength={200} />
        </Form.Item>
        <Form.Item
          name="note"
          label={text.note}
          rules={[partnerRules.optional(text.note, PARTNER_LIMITS.note)]}
        >
          <Input.TextArea rows={2} maxLength={500} showCount />
        </Form.Item>
      </Form>
    </Modal>
  );
}
