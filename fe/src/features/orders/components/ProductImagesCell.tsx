import { Space, Tag, Tooltip } from 'antd';
import { MESSAGES } from '@/constants/messages';
import type { ProductListItem } from '../types';
import { ProductImage } from './ProductImage';

type ProductImagesCellProps = { product: ProductListItem; onOpenGallery: () => void };

/** Cột ảnh của danh sách mã hàng: ảnh chính, kèm "+N" khi còn ảnh phụ (bấm để xem tất cả). */
export function ProductImagesCell({ product, onOpenGallery }: ProductImagesCellProps) {
  const extra = product.extraImages.length;
  return (
    <Space size={4}>
      {product.sampleImageUrl ? <ProductImage value={product.sampleImageUrl} /> : '—'}
      {extra > 0 && (
        <Tooltip title={MESSAGES.orders.viewAllImages}>
          <Tag style={{ cursor: 'pointer', marginInlineEnd: 0 }} onClick={onOpenGallery}>
            {`+${extra}`}
          </Tag>
        </Tooltip>
      )}
    </Space>
  );
}
