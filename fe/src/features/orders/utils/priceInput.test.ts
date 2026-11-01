import { formatPriceInput, formatPriceTyping, parsePriceInput } from './priceInput';

describe('ô đơn giá (VND, số nguyên)', () => {
  it('hiện số nguyên với dấu chấm ngăn nghìn', () => {
    expect(formatPriceInput(15000)).toBe('15.000');
    expect(formatPriceInput('1234567')).toBe('1.234.567');
    expect(formatPriceInput(0)).toBe('0');
    expect(formatPriceInput(undefined)).toBe('');
    expect(formatPriceInput('')).toBe('');
  });

  it('giá cũ có phần lẻ thì hiện làm tròn', () => {
    expect(formatPriceInput(1234.5)).toBe('1.235');
  });

  it('đang gõ: tự chia dấu chấm ngăn nghìn', () => {
    expect(formatPriceTyping('1200')).toBe('1.200');
    expect(formatPriceTyping('1234567')).toBe('1.234.567');
    expect(formatPriceTyping('')).toBe('');
  });

  it('dấu chấm chỉ là dấu ngăn nghìn: 1.2 là 12', () => {
    expect(formatPriceTyping('1.2')).toBe('12');
    expect(formatPriceTyping('1.200')).toBe('1.200');
  });

  it('phần sau dấu phẩy bị bỏ, không ghép vào số nguyên', () => {
    expect(formatPriceTyping('1.234,56')).toBe('1.234');
    expect(parsePriceInput('1.234,56')).toBe('1234');
    expect(formatPriceTyping(',5')).toBe('');
  });

  it('bỏ ký tự lạ và dấu trừ', () => {
    expect(formatPriceTyping('1a-2')).toBe('12');
    expect(parsePriceInput('-5')).toBe('5');
  });

  it('đọc ra chuỗi số nguyên; xóa trắng ô thì rỗng, không thành 0', () => {
    expect(parsePriceInput('1.234.567')).toBe('1234567');
    expect(parsePriceInput('')).toBe('');
    expect(parsePriceInput(undefined)).toBe('');
  });
});
