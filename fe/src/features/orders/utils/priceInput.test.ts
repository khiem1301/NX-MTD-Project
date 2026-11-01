import { formatPriceInput, parsePriceInput } from './priceInput';

describe('ô đơn giá', () => {
  it('hiện dấu chấm ngăn nghìn và dấu phẩy thập phân', () => {
    expect(formatPriceInput(15000)).toBe('15.000');
    expect(formatPriceInput(1234567.5)).toBe('1.234.567,5');
    expect(formatPriceInput(undefined)).toBe('');
  });

  it('đọc đúng phần lẻ, không biến 12,5 thành 125', () => {
    expect(parsePriceInput('12,5')).toBe('12.5');
    expect(parsePriceInput('1.234.567,5')).toBe('1234567.5');
  });

  it('xóa trắng ô thì rỗng, không thành 0', () => {
    expect(parsePriceInput('')).toBe('');
  });
});
