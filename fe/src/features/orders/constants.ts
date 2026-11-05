import type { StatusLabel } from '@/components/common/StatusTag';

export const PRODUCT_STATUS_TAG: Record<string, StatusLabel> = {
  Active: { text: 'Đang sản xuất', color: 'green' },
  Discontinued: { text: 'Ngừng sản xuất', color: 'default' },
};

/** Ảnh phụ tối đa của một mã hàng (khớp ProductImageLimits.MaxExtra ở BE); ảnh chính là sampleImageUrl. */
export const MAX_EXTRA_IMAGES = 5;

/** Ảnh theo màu áo (khớp ProductImageLimits ở BE, DEV-ALL-71): mỗi màu 1-3 ảnh, tối đa 30 màu mỗi lần lưu. */
export const MIN_COLOR_IMAGES = 1;
export const MAX_COLOR_IMAGES = 3;
export const MAX_IMAGE_COLORS = 30;
export const COLOR_MAX_LENGTH = 50;
