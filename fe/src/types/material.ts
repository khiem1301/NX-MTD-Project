/**
 * Thêm / sửa NVL trong danh mục (khớp CreateMaterialDto của BE). Dùng ở kho (danh mục NVL) và ở popup kiểm tra Excel định mức
 * của mã hàng khi thêm NVL chưa có vào danh mục (DEV-ALL-123).
 */
export type SaveMaterialPayload = {
  materialName: string;
  materialType: string;
  unit: string;
  color?: string;
  /** Khổ vải (m), bắt buộc với vải, tối đa 3,5 m (DEV-ALL-102). */
  widthM?: number | null;
};
