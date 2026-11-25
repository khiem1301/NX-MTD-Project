/**
 * Định dạng ô nhập tiền VND kiểu Việt Nam: số nguyên, dấu chấm ngăn nghìn (1.250.000), cho phép dấu trừ đứng đầu khi ô nhận số âm
 * (vd giá trị chốt đơn hàng âm = phải đền hợp đồng). Dấu chấm do ô tự thêm nên khi đọc lại chỉ giữ chữ số và dấu trừ đầu tiên.
 */

const groupThousands = (digits: string): string => digits.replace(/\B(?=(\d{3})+(?!\d))/g, '.');

/** Số chữ số tối đa của phần nguyên khi không truyền trần: 1.000 tỷ. */
export const MONEY_MAX_DIGITS = 13;

type Parsed = { negative: boolean; digits: string };

/** Tách dấu và chữ số; phần sau dấu phẩy bị bỏ (tiền VND không có phần lẻ), số 0 đầu bị bỏ. */
const split = (text: string | undefined, allowNegative: boolean, maxDigits: number): Parsed => {
  const raw = (text ?? '').split(',')[0] ?? '';
  const digits = raw
    .replace(/\D/g, '')
    .replace(/^0+(?=\d)/, '')
    .slice(0, maxDigits);
  return { negative: allowNegative && raw.trim().startsWith('-') && digits !== '0', digits };
};

/** Hiển thị giá trị đã lưu (khi không gõ). Trống / không phải số → chuỗi rỗng. */
export const formatMoneyValue = (value: number | string | undefined): string => {
  if (value === undefined || value === '') return '';
  const number = Number(value);
  if (Number.isNaN(number)) return '';
  const rounded = Math.round(number);
  return `${rounded < 0 ? '-' : ''}${groupThousands(String(Math.abs(rounded)))}`;
};

/** Định dạng ngay khi gõ hoặc dán. Gõ riêng dấu trừ thì giữ lại để người dùng gõ tiếp số âm. */
export const formatMoneyTyping = (
  input: string,
  allowNegative: boolean,
  maxDigits: number,
): string => {
  const { negative, digits } = split(input, allowNegative, maxDigits);
  if (digits === '') return negative ? '-' : '';
  return `${negative ? '-' : ''}${groupThousands(digits)}`;
};

/** Trả chuỗi số chuẩn ("-1234") để xóa trắng ô thì giá trị là rỗng chứ không thành 0. */
export const parseMoneyInput = (
  input: string | undefined,
  allowNegative: boolean,
  maxDigits: number,
): string => {
  const { negative, digits } = split(input, allowNegative, maxDigits);
  if (digits === '') return '';
  return `${negative ? '-' : ''}${digits}`;
};
