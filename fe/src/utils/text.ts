/** Bỏ dấu tiếng Việt và hạ chữ thường để tìm "don hang" ra "Đơn hàng". */
export function normalizeText(text: string): string {
  return text
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'd')
    .toLowerCase();
}
