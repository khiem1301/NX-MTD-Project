import { formatQtyTyping, formatQtyValue, maxIntDigits, parseQtyInput } from './qtyInput';

describe('ô số lượng', () => {
  it('hiển thị giá trị đã lưu với dấu chấm ngăn nghìn, dấu phẩy phần lẻ', () => {
    expect(formatQtyValue(1_000_000, 0)).toBe('1.000.000');
    expect(formatQtyValue(999, 0)).toBe('999');
    expect(formatQtyValue('1234.5', 3)).toBe('1.234,5');
    expect(formatQtyValue(12.5, 3)).toBe('12,5');
    expect(formatQtyValue(100_000_000, 3)).toBe('100.000.000');
    expect(formatQtyValue(undefined, 0)).toBe('');
    expect(formatQtyValue('', 0)).toBe('');
  });

  it('đọc lại chữ trong ô: bỏ dấu chấm, phần lẻ sau dấu phẩy, bỏ số 0 đầu', () => {
    expect(parseQtyInput('1.234.567', 0, Infinity)).toBe('1234567');
    expect(parseQtyInput('1.234,567', 3, Infinity)).toBe('1234.567');
    expect(parseQtyInput('12,34567', 3, Infinity)).toBe('12.345');
    expect(parseQtyInput('12,5', 0, Infinity)).toBe('12');
    expect(parseQtyInput('007', 0, Infinity)).toBe('7');
    expect(parseQtyInput(',5', 3, Infinity)).toBe('0.5');
    expect(parseQtyInput('', 0, Infinity)).toBe('');
    expect(parseQtyInput(undefined, 0, Infinity)).toBe('');
  });

  it('cho vượt trần nhưng cắt chữ số thừa so với số chữ số của trần', () => {
    expect(maxIntDigits(1_000_000)).toBe(7);
    expect(parseQtyInput('2.000.000', 0, 7)).toBe('2000000');
    expect(parseQtyInput('99999999', 0, 7)).toBe('9999999');
    expect(parseQtyInput('13,5', 3, 2)).toBe('13.5');
  });

  it('định dạng ngay khi gõ: thêm dấu chấm, giữ dấu phẩy vừa gõ, cắt chữ số thừa', () => {
    expect(formatQtyTyping('1234567', 0, Infinity)).toBe('1.234.567');
    expect(formatQtyTyping('1.2345', 0, Infinity)).toBe('12.345');
    expect(formatQtyTyping('1234,', 3, Infinity)).toBe('1.234,');
    expect(formatQtyTyping('1234,5', 3, Infinity)).toBe('1.234,5');
    expect(formatQtyTyping('1234,', 0, Infinity)).toBe('1.234');
    expect(formatQtyTyping('20.000.000', 0, 7)).toBe('2.000.000');
    expect(formatQtyTyping('201,', 3, 2)).toBe('20,');
    expect(formatQtyTyping('', 0, Infinity)).toBe('');
  });
});
