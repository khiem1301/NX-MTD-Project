/** Hiển thị đơn giá kiểu Việt Nam: 1.234.567,5. */
export const formatPriceInput = (value: number | string | undefined): string => {
  const [integer = '', decimal] = `${value ?? ''}`.split('.');
  const grouped = integer.replace(/\B(?=(\d{3})+(?!\d))/g, '.');
  return decimal === undefined ? grouped : `${grouped},${decimal}`;
};

/** Trả chuỗi (không phải Number) để xóa trắng ô thì giá trị là rỗng chứ không thành 0. */
export const parsePriceInput = (input: string | undefined): string =>
  (input ?? '').replace(/\./g, '').replace(',', '.');
