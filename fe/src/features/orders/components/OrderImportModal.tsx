import { useState } from 'react';
import { Alert, Modal, Space, Upload } from 'antd';
import { InboxOutlined } from '@ant-design/icons';
import { HintTip } from '@/components/common/HintTip';
import { ImportTemplateLink } from '@/components/common/ImportTemplateLink';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { MESSAGES } from '@/constants/messages';
import { usePreviewOrderImport } from '../hooks/useSpecImport';
import type { OrderImportPreview, OrderImportRow, SaveProductPayload } from '../types';
import type { ProductImportSelection } from '../utils/productExcelImports';
import { OrderImportReviewModal } from './OrderImportReviewModal';

const text = MESSAGES.orders.orderImport;

type OrderImportModalProps = {
  /** Đơn đang sửa (null / bỏ trống khi tạo mới): file mẫu chứa dòng hàng hiện có của đơn. */
  orderId?: number | null;
  onApply: (rows: OrderImportRow[]) => void;
  onClose: () => void;
  /**
   * DEV-ALL-151: khai mã chưa có (chưa ghi). File Excel của mã được giao cho form đơn, nhập sau khi Lưu đơn.
   * Không truyền thì không hiện nút tạo.
   */
  onStageProduct?: (body: SaveProductPayload, selection: ProductImportSelection) => number | null;
  onUnstageProduct?: (tempId: number) => void;
  /** Mã hàng đã khai trên form đơn (chờ Lưu đơn): mã chữ hoa → tempId. Dòng của mã này được nhận luôn, không hiện nút tạo. */
  stagedProducts?: Record<string, number>;
};

/**
 * DEV-ALL-118/178: nhập dòng hàng của đơn từ Excel dạng ma trận màu × size. Chọn file là mở popup kiểm tra: sửa từng ô, máy chủ kiểm
 * lại sau mỗi lần sửa; Áp dụng thì điền các dòng hợp lệ vào form, người dùng xem lại và bấm Lưu như tạo tay. Chỉ gắn khi đang mở.
 */
export function OrderImportModal({
  orderId,
  onApply,
  onClose,
  onStageProduct,
  onUnstageProduct,
  stagedProducts,
}: OrderImportModalProps) {
  const preview = usePreviewOrderImport();
  const [reviewing, setReviewing] = useState<{ file: File; initial: OrderImportPreview } | null>(
    null,
  );

  return (
    <Modal
      open
      width={MODAL_WIDTH.xl}
      title={text.title}
      footer={null}
      onCancel={onClose}
      destroyOnHidden
    >
      <Space direction="vertical" size={16} style={{ width: '100%' }}>
        <HintTip text={text.hint} />
        <ImportTemplateLink kind="order-lines" orderId={orderId} />
        <Upload.Dragger
          maxCount={1}
          accept=".xlsx"
          showUploadList={false}
          disabled={preview.isPending}
          beforeUpload={(next) => {
            preview.mutate(next, { onSuccess: (initial) => setReviewing({ file: next, initial }) });
            return false;
          }}
        >
          <p className="ant-upload-drag-icon">
            <InboxOutlined />
          </p>
          <p>{MESSAGES.orders.specImport.chooseFile}</p>
        </Upload.Dragger>
        {preview.error && <Alert type="error" showIcon message={preview.error.message} />}
      </Space>
      {reviewing && (
        <OrderImportReviewModal
          file={reviewing.file}
          initial={reviewing.initial}
          onApply={onApply}
          onBack={() => setReviewing(null)}
          onStageProduct={onStageProduct}
          onUnstageProduct={onUnstageProduct}
          stagedProducts={stagedProducts}
        />
      )}
    </Modal>
  );
}
