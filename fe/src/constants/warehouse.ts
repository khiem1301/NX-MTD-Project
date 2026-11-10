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
