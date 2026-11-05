import { Flex, Modal, Typography } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { MAX_EXTRA_IMAGES } from '../constants';
import type { ProductListItem } from '../types';
import { ProductImage } from './ProductImage';

type ProductGalleryModalProps = { product: ProductListItem | null; onClose: () => void };

/** Xem tất cả ảnh của một mã hàng: ảnh chính, các ảnh phụ và ảnh theo màu (bấm vào ảnh để phóng to). */
export function ProductGalleryModal({ product, onClose }: ProductGalleryModalProps) {
  const text = MESSAGES.orders;
  return (
    <Modal
      open={product !== null}
      title={
        product
          ? `${text.galleryTitle}: ${product.productCode} - ${product.productName}`
          : text.galleryTitle
      }
      footer={null}
      width={720}
      onCancel={onClose}
      destroyOnHidden
    >
      {product && (
        <>
          <Typography.Title level={5}>{text.sampleImage}</Typography.Title>
          {product.sampleImageUrl ? (
            <ProductImage value={product.sampleImageUrl} size={160} />
          ) : (
            <Typography.Text type="secondary">—</Typography.Text>
          )}
          <Typography.Title level={5} style={{ marginTop: 16 }}>
            {`${text.extraImages} (${product.extraImages.length}/${MAX_EXTRA_IMAGES})`}
          </Typography.Title>
          <Flex wrap gap={8}>
            {product.extraImages.map((key) => (
              <ProductImage key={key} value={key} size={120} />
            ))}
          </Flex>
          <Typography.Title level={5} style={{ marginTop: 16 }}>
            {text.colorImages}
          </Typography.Title>
          {product.colorImages.length === 0 && (
            <Typography.Text type="secondary">{text.noColorImages}</Typography.Text>
          )}
          {product.colorImages.map((row) => (
            <div key={row.color} style={{ marginBottom: 12 }}>
              <Typography.Text strong>{row.color}</Typography.Text>
              <Flex wrap gap={8} style={{ marginTop: 4 }}>
                {row.images.map((key) => (
                  <ProductImage key={key} value={key} size={120} />
                ))}
              </Flex>
            </div>
          ))}
        </>
      )}
    </Modal>
  );
}
