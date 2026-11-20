import { normalizeCode } from '@/utils/textInput';

/** Vải luôn tính theo mét, khớp MaterialTypes.FabricUnit ở BE. */
export const FABRIC_UNIT = 'm';
/** Độ dài Mã NVL / Tên NVL, khớp `MaterialLimits` ở BE. */
export const MATERIAL_CODE_MAX_LENGTH = 30;
export const MATERIAL_NAME_MAX_LENGTH = 200;

/** Ô Mã NVL: chỉ chữ không dấu, số và . _ - (kể cả khi dán), tự đổi chữ hoa như BE lưu. */
export const normalizeMaterialCode = (value: string | undefined) =>
  normalizeCode(value)?.toUpperCase();
