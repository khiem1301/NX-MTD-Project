/** Ảnh tải lên: tối đa 5 MB, chỉ nhận JPG, PNG, WebP như máy chủ (GIF, HEIC bị từ chối ngay khi chọn). Máy chủ vẫn kiểm lại chữ ký tệp. */
export const MAX_IMAGE_BYTES = 5 * 1024 * 1024;

const ACCEPTED_TYPES = new Set(['image/jpeg', 'image/png', 'image/webp']);

export const isAcceptableImage = (file: { type: string; size: number }): boolean =>
  ACCEPTED_TYPES.has(file.type) && file.size <= MAX_IMAGE_BYTES;
