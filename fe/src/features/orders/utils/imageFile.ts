/** Ảnh tải lên: tối đa 5 MB, chỉ nhận tệp ảnh (máy chủ vẫn kiểm lại chữ ký tệp). */
export const MAX_IMAGE_BYTES = 5 * 1024 * 1024;

export const isAcceptableImage = (file: { type: string; size: number }): boolean =>
  file.type.startsWith('image/') && file.size <= MAX_IMAGE_BYTES;
