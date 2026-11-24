import { useEffect } from 'react';
import { Col, Form, Input, Modal, Row, Tooltip } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import { zodRule } from '@/utils/zodRule';
import { useProduct, useSaveProduct } from '../hooks/useOrders';
import { useProductFormImports } from '../hooks/useProductFormImports';
import {
  productCodeSchema,
  productDescriptionSchema,
  toFieldPath,
} from '../utils/productValidation';
import { productCodeExists } from '../utils/productCodeCheck';
import type { ProductImportSelection } from '../utils/productExcelImports';
import { ProductExcelImports } from './ProductExcelImports';
import { ProductImagesFields } from './ProductImagesFields';
import type { NamePath } from 'antd/es/form/interface';
import type { ProductListItem, SaveProductPayload } from '../types';

const MODAL_WIDTH = 820;
const BODY_MAX_HEIGHT = 'calc(100vh - 170px)';

type ProductFormModalProps = {
  open: boolean;
  product: ProductListItem | null;
  onClose: () => void;
  /** Gọi sau khi lưu xong, kèm Id (dùng để chọn luôn mã hàng vừa tạo ở form đơn hàng). */
  onSaved?: (productId: number, code: string) => void;
  /** DEV-ALL-121: hiện mục chọn file Excel (Thành phần, Định mức NVL, Nhóm bìa, Bìa cắt, NVL của đơn) — chỉ màn Mã hàng (Admin). */
  allowImports?: boolean;
  /**
   * Form mở từ màn tạo / sửa đơn: không nhập ngay mà giao file đã chọn cho form đơn, nhập sau khi Lưu đơn (lúc đó đơn đã có
   * mã hàng, màu, size nên nhập được cả định mức, nhóm bìa, bìa cắt, NVL của đơn).
   */
  onDeferImports?: (productId: number, code: string, selection: ProductImportSelection) => void;
  /**
   * DEV-ALL-151: form mở từ đơn hàng. Lưu không ghi mã hàng; giao dữ liệu cho form đơn, chỉ ghi khi đơn được lưu.
   * Trả false thì giữ hộp thoại (vd mã đã có trên đơn).
   */
  onStage?: (body: SaveProductPayload, selection: ProductImportSelection) => boolean | void;
  /** Thêm mới: giá trị điền sẵn (vd mã, tên, mô tả đọc từ file Excel của hộp nhập dòng hàng). */
  initialValues?: Partial<SaveProductPayload>;
  /**
   * Thêm mới: dữ liệu Excel đã đọc sẵn (vd từ hộp Nhập dòng hàng của form đơn); popup kiểm tra mở ngay để xem và sửa. Chỉ đọc khi
   * form được gắn, nên nơi dùng gắn form riêng mỗi lần mở.
   */
  initialImports?: ProductImportSelection;
  /** Có initialImports: mở popup kiểm tra ngay (mặc định). Tắt khi mở lại mã hàng đã khai trên đơn để xem / sửa. */
  autoReviewImports?: boolean;
  /** Tiêu đề thay cho "Thêm mới" (vd mở lại mã hàng khai trên đơn). */
  title?: string;
};

/** UC10: thêm / sửa mã hàng. Mã khóa khi đã dùng trong đơn. Ảnh mẫu chọn từ máy, lưu khóa file trong SampleImageUrl (DEV-KH-08-1). */
export function ProductFormModal({
  open,
  product,
  onClose,
  onSaved,
  allowImports = false,
  onDeferImports,
  onStage,
  initialValues,
  initialImports,
  autoReviewImports = true,
  title,
}: ProductFormModalProps) {
  const [form] = Form.useForm<SaveProductPayload>();
  const save = useSaveProduct();
  const excel = useProductFormImports({
    form,
    product,
    allowImports,
    onDeferImports,
    initialImports,
    onClose,
  });
  const { importing, close } = excel;
  const busy = save.isPending || importing;
  // Danh sách không có mô tả: phải nạp từ chi tiết, nếu không lưu lại sẽ xóa mất mô tả cũ.
  const detail = useProduct(product?.productId, open && product !== null);
  const loadingDetail = product !== null && !detail.data;

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    if (product) {
      form.setFieldsValue({
        productCode: product.productCode,
        sampleImageUrl: product.sampleImageUrl ?? undefined,
        extraImages: product.extraImages,
      });
    } else if (initialValues) {
      form.setFieldsValue(initialValues);
    }
  }, [open, product, form, initialValues]);

  // Điền mô tả khi mở form (chi tiết đã có sẵn trong bộ nhớ đệm) hoặc khi chi tiết về sau, trừ khi người dùng đã tự gõ.
  useEffect(() => {
    if (open && detail.data && !form.isFieldTouched('description'))
      form.setFieldsValue({ description: detail.data.description ?? undefined });
  }, [open, product, detail.data, form]);

  const handleFinish = async (values: SaveProductPayload) => {
    // DEV-ALL-124: Thêm mới thiếu dữ liệu Excel bắt buộc thì không lưu gì.
    if (!excel.checkRequired()) return;
    // Thêm mới: dữ liệu Excel còn dòng lỗi / thiếu trường thì không lưu, ở lại form để sửa.
    if (!(await excel.checkNoErrors())) return;
    if (onStage) {
      const accepted = onStage(
        { ...values, productCode: values.productCode.trim().toUpperCase() },
        excel.imports,
      );
      if (accepted !== false) close();
      return;
    }
    save.mutate(
      { id: product?.productId, body: values },
      {
        onSuccess: (id) => {
          const productId = product?.productId ?? (id as number);
          const code = values.productCode.trim().toUpperCase();
          onSaved?.(productId, code);
          excel.afterSave(productId, code);
        },
        onError: (error) =>
          form.setFields(
            // Lỗi theo field (vd "extraImages[0]") hiện ngay dưới ô tương ứng. Tên field do BE trả về
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
      title={
        title ??
        (product ? `${MESSAGES.common.edit} ${product.productCode}` : MESSAGES.common.create)
      }
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      width={MODAL_WIDTH}
      confirmLoading={busy}
      okButtonProps={{ disabled: loadingDetail }}
      cancelButtonProps={{ disabled: importing }}
      closable={!importing}
      maskClosable={!importing}
      onOk={() => form.submit()}
      onCancel={() => {
        if (!importing) close();
      }}
      destroyOnHidden
      // Thân modal tự cuộn để nút Lưu luôn hiện, kể cả khi khai nhiều màu.
      styles={{
        body: {
          maxHeight: BODY_MAX_HEIGHT,
          overflowY: 'auto',
          overflowX: 'hidden',
          // Chừa lề dưới để khung Nhập từ Excel không bị cắt sát mép vùng cuộn.
          paddingBottom: 4,
        },
      }}
    >
      {detail.error && <QueryError error={detail.error} onRetry={() => void detail.refetch()} />}
      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={busy}>
        <Row gutter={16}>
          <Col xs={24} md={9}>
            <Tooltip title={locked ? MESSAGES.orders.codeLockedHint : undefined}>
              <Form.Item
                name="productCode"
                label={MESSAGES.orders.productCode}
                required
                rules={[
                  zodRule(productCodeSchema),
                  // Thêm mới: báo ngay mã đã có trong hệ thống (lúc rời ô và khi bấm Lưu), không đợi máy chủ từ chối.
                  {
                    validator: async (_, value: unknown) => {
                      const code = typeof value === 'string' ? value.trim() : '';
                      if (product !== null || !code || !productCodeSchema.safeParse(code).success)
                        return;
                      if (await productCodeExists(code))
                        throw new Error(
                          fill(MESSAGES.orders.productCodeExists, code.toUpperCase()),
                        );
                    },
                    validateTrigger: ['onBlur'],
                  },
                ]}
              >
                <Input disabled={locked} />
              </Form.Item>
            </Tooltip>
          </Col>
          <Col xs={24} md={15}>
            <Form.Item
              name="description"
              label={MESSAGES.orders.description}
              rules={[zodRule(productDescriptionSchema)]}
            >
              <Input.TextArea autoSize={{ minRows: 1, maxRows: 4 }} />
            </Form.Item>
          </Col>
        </Row>
        <ProductImagesFields />
      </Form>
      {allowImports && (
        <ProductExcelImports
          productId={product?.productId}
          deferred={onDeferImports !== undefined || onStage !== undefined}
          required={excel.required}
          showMissing={excel.showMissing}
          onProductRow={excel.fillFromRow}
          autoReview={autoReviewImports && initialImports !== undefined && product === null}
          value={excel.imports}
          onChange={excel.setImports}
          disabled={busy}
        />
      )}
    </Modal>
  );
}
