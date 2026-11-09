/** NVL gõ tay (chưa có trong danh mục) được mã hóa thành "new:<tên>" trong giá trị của ô chọn NVL. */
export const NEW_MATERIAL_PREFIX = 'new:';

export const isNewMaterial = (value: unknown): value is string =>
  typeof value === 'string' && value.startsWith(NEW_MATERIAL_PREFIX);

export const newMaterialName = (value: string): string => value.slice(NEW_MATERIAL_PREFIX.length);
