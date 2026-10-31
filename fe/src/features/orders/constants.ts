import type { StatusLabel } from '@/components/common/StatusTag';

export const PRODUCT_STATUS_TAG: Record<string, StatusLabel> = {
  Active: { text: 'Đang sản xuất', color: 'green' },
  Discontinued: { text: 'Ngừng sản xuất', color: 'default' },
};
