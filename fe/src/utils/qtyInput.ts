/**
 * Định dạng ô nhập số lượng kiểu Việt Nam: dấu chấm ngăn nghìn (1.250.000), dấu phẩy trước phần lẻ (12,5).
 * Dấu chấm do ô tự thêm nên khi đọc lại chỉ giữ chữ số và dấu phẩy đầu tiên.
 */

const groupThousands = (digits: string): string => digits.replace(/\B(?=(\d{3})+(?!\d))/g, '.');

/** Chuỗi số dạng "1234.5" → "1.234,5". */
const display = (normalized: string): string => {
  const [int = '', frac] = normalized.split('.');
  return frac === undefined ? groupThousands(int) : `${groupThousands(int)},${frac}`;
};

/** Số ra chuỗi không dùng dạng mũ, bỏ số 0 thừa ở phần lẻ: 12.50 → "12.5". */
const toPlain = (value: number, decimals: number): string =>
  decimals > 0 ? String(Number(value.toFixed(decimals))) : String(Math.round(value));

/** Hiển thị giá trị đã lưu (khi không gõ). Trống / không phải số → chuỗi rỗng. */
export const formatQtyValue = (value: number | string | undefined, decimals: number): string => {
  if (value === undefined || value === '') return '';
  const number = Number(value);
  return Number.isNaN(number) ? '' : display(toPlain(number, decimals));
};

/** Số chữ số phần nguyên của trần: 1.000.000 → 7. Ô cho gõ vượt trần nhưng không dài hơn số chữ số này. */
export const maxIntDigits = (max: number): number => String(Math.trunc(Math.abs(max))).length;

/**
 * Đọc chữ trong ô thành chuỗi số chuẩn ("1234.5"): bỏ dấu chấm và ký tự lạ, phần lẻ sau dấu phẩy đầu tiên
 * (cắt theo `decimals`, ô số nguyên thì bỏ). Phần nguyên dài quá `maxDigits` chữ số thì cắt chữ số thừa
 * (không cắt về trần: được gõ vượt trần, chỉ bị chặn thêm chữ số). Trả chuỗi (không phải Number)
 * để xóa trắng ô thì giá trị là rỗng chứ không thành 0.
 */
export const parseQtyInput = (
  text: string | undefined,
  decimals: number,
  maxDigits: number,
): string => {
  const [intPart = '', ...rest] = (text ?? '').split(',');
  const int = intPart
    .replace(/\D/g, '')
    .replace(/^0+(?=\d)/, '')
    .slice(0, maxDigits);
  const frac = decimals > 0 ? rest.join('').replace(/\D/g, '').slice(0, decimals) : '';
  if (!int && !frac) return '';
  return frac ? `${int || '0'}.${frac}` : int;
};

/** Định dạng ngay khi gõ hoặc dán: thêm dấu chấm ngăn nghìn, giữ dấu phẩy vừa gõ của ô có phần lẻ. */
export const formatQtyTyping = (input: string, decimals: number, maxDigits: number): string => {
  const parsed = parseQtyInput(input, decimals, maxDigits);
  if (parsed === '') return '';
  const trailingComma = decimals > 0 && !parsed.includes('.') && input.includes(',');
  return display(parsed) + (trailingComma ? ',' : '');
};
