import type { StatusLabel } from '@/components/common/StatusTag';

export const PRODUCT_STATUS_TAG: Record<string, StatusLabel> = {
  Active: { text: 'Đang sản xuất', color: 'green' },
  Discontinued: { text: 'Ngừng sản xuất', color: 'default' },
};

/** Ảnh phụ tối đa của một mã hàng (khớp ProductImageLimits.MaxExtra ở BE); ảnh chính là sampleImageUrl. */
export const MAX_EXTRA_IMAGES = 5;

export const COLOR_MAX_LENGTH = 50;

/** File mẫu nhập Excel định mức (DEV-ALL-115), nằm trong fe/public/templates. */
export const SPEC_IMPORT_TEMPLATE_URL = '/templates/Mau_Nhap_Dinh_Muc.xlsx';

// ---- DEV-ALL-122/123: popup kiểm tra file Excel nhiều sheet ----

/** Kết quả kiểm một dòng (Action của BE): Pending / NewMaterial chỉ có ở popup kiểm tra; Exists là mã hàng đã có. */
export const EXCEL_ROW_ACTION = {
  NEW: 'New',
  UPDATE: 'Update',
  UNCHANGED: 'Unchanged',
  ERROR: 'Error',
  PENDING: 'Pending',
  NEW_MATERIAL: 'NewMaterial',
  EXISTS: 'Exists',
} as const;

/** Loại chi tiết trong file Excel (mã như BE nhận): PANEL = vải cắt, TRIM = phụ liệu. */
export const EXCEL_COMPONENT_TYPE = { PANEL: 'PANEL', TRIM: 'TRIM' } as const;

/** Trạng thái bìa trong file Excel (mã như BE nhận). */
export const EXCEL_MARKER_STATUS = { ACTIVE: 'ACTIVE', DISCONTINUED: 'DISCONTINUED' } as const;

/** Gợi ý khi thêm NVL chưa có vào danh mục: đơn vị, khổ mặc định theo loại (vải: mét, khổ 1,5 m). */
export const NEW_MATERIAL_DEFAULTS = {
  FABRIC: { unit: 'm', widthM: 1.5 },
  TRIM: { unit: 'cái', widthM: null },
} as const;

/** Khổ vải tối đa (m), khớp validator BE (DEV-ALL-102). */
export const MAX_FABRIC_WIDTH_M = 3.5;

/** Màu biểu tượng file Excel trên khung nhập. */
export const EXCEL_ICON_COLOR = '#217346';
