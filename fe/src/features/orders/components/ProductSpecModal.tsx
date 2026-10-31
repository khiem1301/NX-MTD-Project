import { useRef } from 'react';
import { Alert, Modal, Spin } from 'antd';
import { useProduct, useProductSpec } from '../hooks/useOrders';
import type { ProductListItem } from '../types';
import { DetailModal } from '@/components/common/DetailModal';
import { ProductSpecEditor } from './ProductSpecEditor';
import { MESSAGES } from '@/constants/messages';

const text = MESSAGES.orders.spec;

type Props = { product: ProductListItem | null; showHistory: boolean; onClose: () => void };

/**
 * UC10 (định mức): khai báo một lần cho mã hàng — thành phần (chi tiết cắt / phụ liệu), các biến thể màu × size và NVL của
 * từng thành phần. Một dòng định mức áp dụng cho nhiều màu – size cùng lúc; máy chủ lưu theo từng biến thể.
 */
export function ProductSpecModal({ product, showHistory, onClose }: Props) {
  const spec = useProductSpec(product?.productId);
  const detail = useProduct(product?.productId, showHistory);
  const dirty = useRef(false);

  /** Còn thay đổi chưa lưu thì hỏi lại trước khi đóng. */
  const requestClose = () => {
    if (!dirty.current) return onClose();
    Modal.confirm({
      title: text.unsavedTitle,
      content: text.unsavedBody,
      okText: text.leave,
      cancelText: text.stay,
      okButtonProps: { danger: true },
      onOk: onClose,
    });
  };
  return (
    <DetailModal
      open={product !== null}
      title={
        product ? `${text.title}: ${product.productCode} - ${product.productName}` : text.title
      }
      onClose={requestClose}
      size="xl"
    >
      {spec.error && (
        <Alert type="error" showIcon message={spec.error.message} style={{ marginBottom: 12 }} />
      )}
      {spec.isLoading && <Spin />}
      {product && spec.data && (
        <ProductSpecEditor
          key={spec.dataUpdatedAt}
          product={product}
          spec={spec.data}
          detail={detail.data}
          showHistory={showHistory}
          onDirtyChange={(v) => (dirty.current = v)}
          onClose={requestClose}
        />
      )}
    </DetailModal>
  );
}
