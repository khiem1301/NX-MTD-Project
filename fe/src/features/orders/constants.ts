import type { StatusLabel } from '@/components/common/StatusTag';

export const PRODUCT_STATUS_TAG: Record<string, StatusLabel> = {
  Active: { text: 'Đang sản xuất', color: 'green' },
  Discontinued: { text: 'Ngừng sản xuất', color: 'default' },
};

/** Ảnh phụ tối đa của một mã hàng (khớp ProductImageLimits.MaxExtra ở BE); ảnh chính là sampleImageUrl. */
export const MAX_EXTRA_IMAGES = 5;

export const COLOR_MAX_LENGTH = 50;
