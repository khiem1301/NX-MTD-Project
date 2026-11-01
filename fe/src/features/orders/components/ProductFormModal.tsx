import { useEffect } from 'react';
import { Form, Input, Modal, Tooltip } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { zodRule } from '@/utils/zodRule';
import { useProduct, useSaveProduct } from '../hooks/useOrders';
import {
  productCodeSchema,
  productDescriptionSchema,
  productNameSchema,
} from '../utils/productValidation';
import { ExtraImagesInput } from './ExtraImagesInput';
import { SampleImageInput } from './SampleImageInput';
import type { ProductListItem, SaveProductPayload } from '../types';

type ProductFormModalProps = {
  open: boolean;
  product: ProductListItem | null;
  onClose: () => void;
  /** Gọi sau khi lưu xong, kèm Id (dùng để chọn luôn mã hàng vừa tạo ở form đơn hàng). */
  onSaved?: (productId: number, code: string) => void;
};

/** UC10: thêm / sửa mã hàng. Mã khóa khi đã dùng trong đơn. Ảnh mẫu chọn từ máy, lưu khóa file trong SampleImageUrl (DEV-KH-08-1). */
export function ProductFormModal({ open, product, onClose, onSaved }: ProductFormModalProps) {
  const [form] = Form.useForm<SaveProductPayload>();
  const save = useSaveProduct();
  // Danh sách không có mô tả: phải nạp từ chi tiết, nếu không lưu lại sẽ xóa mất mô tả cũ.
  const detail = useProduct(product?.productId, open && product !== null);
  const loadingDetail = product !== null && !detail.data;

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    if (product) {
      form.setFieldsValue({
        productCode: product.productCode,
        productName: product.productName,
        sampleImageUrl: product.sampleImageUrl ?? undefined,
        extraImages: product.extraImages,
      });
    }
  }, [open, product, form]);

  // Điền mô tả khi mở form (chi tiết đã có sẵn trong bộ nhớ đệm) hoặc khi chi tiết về sau, trừ khi người dùng đã tự gõ.
  useEffect(() => {
    if (open && detail.data && !form.isFieldTouched('description'))
      form.setFieldsValue({ description: detail.data.description ?? undefined });
  }, [open, product, detail.data, form]);

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
            Object.entries(error.errors ?? {}).map(([name, errors]) => ({
              name: name as keyof SaveProductPayload,
              errors,
            })),
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
      width={640}
      confirmLoading={save.isPending}
      okButtonProps={{ disabled: loadingDetail }}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      {detail.error && <QueryError error={detail.error} onRetry={() => void detail.refetch()} />}
      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={save.isPending}>
        <Tooltip title={locked ? MESSAGES.orders.codeLockedHint : undefined}>
          <Form.Item
            name="productCode"
            label={MESSAGES.orders.productCode}
            required
            rules={[zodRule(productCodeSchema)]}
          >
            <Input disabled={locked} />
          </Form.Item>
        </Tooltip>
        <Form.Item
          name="productName"
          label={MESSAGES.orders.productName}
          required
          rules={[zodRule(productNameSchema)]}
        >
          <Input />
        </Form.Item>
        <Form.Item name="sampleImageUrl" label={MESSAGES.orders.sampleImage} rules={[{ max: 500 }]}>
          <SampleImageInput />
        </Form.Item>
        <Form.Item name="extraImages" label={MESSAGES.orders.extraImages}>
          <ExtraImagesInput />
        </Form.Item>
        <Form.Item
          name="description"
          label={MESSAGES.orders.description}
          rules={[zodRule(productDescriptionSchema)]}
        >
          <Input.TextArea rows={2} />
        </Form.Item>
      </Form>
    </Modal>
  );
}
