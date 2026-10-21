import { useEffect } from 'react';
import { Form, Input, Modal, Tooltip } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { useSaveProduct } from '../hooks/useOrders';
import type { ProductListItem, SaveProductPayload } from '../types';

type ProductFormModalProps = {
  open: boolean;
  product: ProductListItem | null;
  onClose: () => void;
  /** Gọi sau khi lưu xong, kèm Id (dùng để chọn luôn mã hàng vừa tạo ở form đơn hàng). */
  onSaved?: (productId: number, code: string) => void;
};

/** UC10: thêm / sửa mã hàng. Mã khóa khi đã dùng trong đơn. Ảnh mẫu là đường dẫn (DEV-KH-08-1). */
export function ProductFormModal({ open, product, onClose, onSaved }: ProductFormModalProps) {
  const [form] = Form.useForm<SaveProductPayload>();
  const save = useSaveProduct();

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    if (product) {
      form.setFieldsValue({
        productCode: product.productCode,
        productName: product.productName,
        sampleImageUrl: product.sampleImageUrl ?? undefined,
      });
    }
  }, [open, product, form]);

  const handleFinish = (values: SaveProductPayload) => {
    save.mutate(
      { id: product?.productId, body: values },
      {
        onSuccess: (id) => {
          onSaved?.(id as number, values.productCode.trim().toUpperCase());
          onClose();
        },
        onError: (error) =>
          form.setFields(
            Object.entries(error.errors ?? {}).map(([name, errors]) => ({ name: name as keyof SaveProductPayload, errors })),
          ),
      },
    );
  };

  const locked = product?.isUsed === true;

  return (
    <Modal
      open={open}
      title={product ? `${MESSAGES.common.edit} ${product.productCode}` : MESSAGES.common.create}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={save.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={save.isPending}>
        <Tooltip title={locked ? MESSAGES.orders.codeLockedHint : undefined}>
          <Form.Item
            name="productCode"
            label={MESSAGES.orders.productCode}
            rules={[{ required: true, whitespace: true, message: MESSAGES.common.required }, { max: 30 }]}
          >
            <Input disabled={locked} />
          </Form.Item>
        </Tooltip>
        <Form.Item
          name="productName"
          label={MESSAGES.orders.productName}
          rules={[{ required: true, whitespace: true, message: MESSAGES.common.required }, { max: 200 }]}
        >
          <Input />
        </Form.Item>
        <Form.Item name="sampleImageUrl" label={MESSAGES.orders.sampleImage} rules={[{ max: 500 }, { type: 'url' }]}>
          <Input placeholder="https://..." />
        </Form.Item>
        <Form.Item name="description" label={MESSAGES.orders.description} rules={[{ max: 1000 }]}>
          <Input.TextArea rows={2} />
        </Form.Item>
      </Form>
    </Modal>
  );
}
