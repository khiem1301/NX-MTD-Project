const groupThousands = (digits: string): string => digits.replace(/\B(?=(\d{3})+(?!\d))/g, '.');

/** Hiển thị đơn giá VND: số nguyên, dấu chấm ngăn nghìn (1.234.567). */
export const formatPriceInput = (value: number | string | undefined): string => {
  const number = Number(value);
  return value === undefined || value === '' || Number.isNaN(number)
    ? ''
    : groupThousands(`${Math.round(number)}`);
};

/**
 * Định dạng ngay khi đang gõ hoặc dán: đơn giá VND không có phần lẻ nên chỉ giữ chữ số rồi chia dấu chấm ngăn nghìn.
 * Phần sau dấu phẩy đầu tiên bị bỏ (dán `1.234,56` ra `1.234`), không ghép vào số nguyên.
 */
export const formatPriceTyping = (input: string): string =>
  groupThousands((input.split(',')[0] ?? '').replace(/\D/g, ''));

/** Trả chuỗi (không phải Number) để xóa trắng ô thì giá trị là rỗng chứ không thành 0. */
export const parsePriceInput = (input: string | undefined): string =>
  (input ?? '').split(',')[0]?.replace(/\D/g, '') ?? '';
