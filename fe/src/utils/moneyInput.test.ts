import {
  formatMoneyTyping,
  formatMoneyValue,
  MONEY_MAX_DIGITS,
  parseMoneyInput,
} from './moneyInput';

describe('moneyInput', () => {
  it('hiện dấu chấm ngăn nghìn khi quá 3 chữ số', () => {
    expect(formatMoneyValue(999)).toBe('999');
    expect(formatMoneyValue(1000)).toBe('1.000');
    expect(formatMoneyValue(1234567)).toBe('1.234.567');
    expect(formatMoneyValue(-5000000)).toBe('-5.000.000');
  });

  it('làm tròn phần lẻ, giá trị trống hoặc không phải số thì rỗng', () => {
    expect(formatMoneyValue(1500.6)).toBe('1.501');
    expect(formatMoneyValue(undefined)).toBe('');
    expect(formatMoneyValue('abc')).toBe('');
  });

  it('định dạng ngay khi gõ, bỏ ký tự lạ và phần sau dấu phẩy', () => {
    expect(formatMoneyTyping('1234', false, MONEY_MAX_DIGITS)).toBe('1.234');
    expect(formatMoneyTyping('1.234.5', false, MONEY_MAX_DIGITS)).toBe('12.345');
    expect(formatMoneyTyping('1.234,56', false, MONEY_MAX_DIGITS)).toBe('1.234');
    expect(formatMoneyTyping('12ab34', false, MONEY_MAX_DIGITS)).toBe('1.234');
    expect(formatMoneyTyping('', false, MONEY_MAX_DIGITS)).toBe('');
  });

  it('chỉ nhận dấu trừ khi cho phép số âm; gõ riêng dấu trừ thì giữ lại', () => {
    expect(formatMoneyTyping('-1234', true, MONEY_MAX_DIGITS)).toBe('-1.234');
    expect(formatMoneyTyping('-', true, MONEY_MAX_DIGITS)).toBe('-');
    expect(formatMoneyTyping('-1234', false, MONEY_MAX_DIGITS)).toBe('1.234');
    expect(parseMoneyInput('-1.234', true, MONEY_MAX_DIGITS)).toBe('-1234');
    expect(parseMoneyInput('-1.234', false, MONEY_MAX_DIGITS)).toBe('1234');
  });

  it('cắt chữ số thừa quá số chữ số tối đa, số 0 đầu bị bỏ, -0 không âm', () => {
    expect(parseMoneyInput('123456', false, 4)).toBe('1234');
    expect(parseMoneyInput('0001', false, 4)).toBe('1');
    expect(parseMoneyInput('-0', true, 4)).toBe('0');
    expect(parseMoneyInput('', true, 4)).toBe('');
  });
});
