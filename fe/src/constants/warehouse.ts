// Giá trị khớp CHECK constraint DB / hằng số BE (Domain/Constants/WarehouseConstants.cs).
export const WAREHOUSE_TYPE = {
  MATERIAL: 'MATERIAL',
  SEMI_FINISHED: 'SEMI_FINISHED',
  FINISHED: 'FINISHED',
} as const;

export const WAREHOUSE_TYPE_LABEL: Record<string, string> = {
  MATERIAL: 'Kho NVL',
  SEMI_FINISHED: 'Kho BTP',
  FINISHED: 'Kho thành phẩm',
};

// Loại NVL (MaterialTypes của BE): dùng ở kho và ở popup kiểm tra Excel định mức (thêm NVL chưa có, DEV-ALL-123).

/** SCRAP: NVL phế phẩm (vải vụn, kim gãy…), chỉ để tổ cắt / tổ may trả về kho, đơn vị tự chọn (DEV-ALL-112). */
export const MATERIAL_TYPE = { FABRIC: 'FABRIC', TRIM: 'TRIM', SCRAP: 'SCRAP' } as const;

export const MATERIAL_TYPE_LABEL: Record<string, string> = {
  FABRIC: 'Vải',
  TRIM: 'Phụ liệu',
  SCRAP: 'Phế phẩm',
};

/** Vải luôn tính theo mét (MaterialTypes.FabricUnit của BE). */
export const FABRIC_UNIT = 'm';
