import { useEffect } from 'react';
import { Col, Divider, Form, Input, Modal, Row, Tooltip } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { zodRule } from '@/utils/zodRule';
import { useProduct, useSaveProduct } from '../hooks/useOrders';
import {
  productCodeSchema,
  productDescriptionSchema,
  productNameSchema,
  toFieldPath,
} from '../utils/productValidation';
import { ColorImagesField } from './ColorImagesField';
import { ExtraImagesInput } from './ExtraImagesInput';
import { SampleImageInput } from './SampleImageInput';
import type { NamePath } from 'antd/es/form/interface';
import type { ProductListItem, SaveProductPayload } from '../types';

const MODAL_WIDTH = 820;
const BODY_MAX_HEIGHT = 'calc(100vh - 240px)';

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
        colorImages: product.colorImages,
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
            // Lỗi của từng màu (vd "colorImages[0].images") hiện ngay dưới ô của màu đó. Tên field do BE trả về
            // lúc chạy nên không kiểm được kiểu khi biên dịch.
            Object.entries(error.errors ?? {}).map(([name, errors]) => ({
              name: toFieldPath(name) as NamePath<SaveProductPayload>,
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
      width={MODAL_WIDTH}
      confirmLoading={save.isPending}
      okButtonProps={{ disabled: loadingDetail }}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
      // Thân modal tự cuộn để nút Lưu luôn hiện, kể cả khi khai nhiều màu.
      styles={{ body: { maxHeight: BODY_MAX_HEIGHT, overflowY: 'auto', overflowX: 'hidden' } }}
    >
      {detail.error && <QueryError error={detail.error} onRetry={() => void detail.refetch()} />}
      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={save.isPending}>
        <Row gutter={16}>
          <Col xs={24} md={9}>
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
          </Col>
          <Col xs={24} md={15}>
            <Form.Item
              name="productName"
              label={MESSAGES.orders.productName}
              required
              rules={[zodRule(productNameSchema)]}
            >
              <Input />
            </Form.Item>
          </Col>
        </Row>
        <Form.Item
          name="description"
          label={MESSAGES.orders.description}
          rules={[zodRule(productDescriptionSchema)]}
        >
          <Input.TextArea autoSize={{ minRows: 1, maxRows: 4 }} />
        </Form.Item>
        <Divider orientation="left" orientationMargin={0} style={{ marginTop: 0 }}>
          {MESSAGES.orders.images}
        </Divider>
        <Row gutter={16}>
          <Col xs={24} md={9}>
            <Form.Item
              name="sampleImageUrl"
              label={MESSAGES.orders.sampleImage}
              rules={[{ max: 500 }]}
            >
              <SampleImageInput />
            </Form.Item>
          </Col>
          <Col xs={24} md={15}>
            <Form.Item name="extraImages" label={MESSAGES.orders.extraImages}>
              <ExtraImagesInput />
            </Form.Item>
          </Col>
        </Row>
        <ColorImagesField suggestions={detail.data?.colors} />
      </Form>
    </Modal>
  );
}
