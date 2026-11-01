import { deliveryCountProblem, MAX_QTY, noteProblem, qtyProblem } from './orderMaterialRules';

describe('qtyProblem', () => {
  it.each([0, 0.001, 1, 99_999_999.999, MAX_QTY, 1234.5])('%s hợp lệ', (v) => {
    expect(qtyProblem('Số cần', v)).toBeNull();
  });

  it('chưa nhập thì không báo (bắt buộc do rule khác)', () => {
    expect(qtyProblem('Số cần', null)).toBeNull();
    expect(qtyProblem('Số cần', undefined)).toBeNull();
  });

  it.each([-0.001, -1])('%s bị báo âm', (v) => {
    expect(qtyProblem('Số cần', v)).toBe('Số cần không được âm.');
  });

  it.each([MAX_QTY + 0.001, MAX_QTY + 1, 1e15, 1e30, Infinity])('%s bị báo vượt giới hạn', (v) => {
    expect(qtyProblem('Số khách cấp', v)).toBe('Số khách cấp tối đa 100.000.000.');
  });

  it.each([1.2345, 0.0004, 0.0001, 10.0005])('%s bị báo quá 3 số lẻ', (v) => {
    expect(qtyProblem('Số cần', v)).toBe('Số cần tối đa 3 số lẻ.');
  });

  it('sai số dấu phẩy động không bị báo nhầm: 0,1 + 0,2 vẫn là 0,3', () => {
    expect(qtyProblem('Số cần', 1.005)).toBeNull();
    expect(qtyProblem('Số cần', 0.1 + 0.2)).toBeNull();
    expect(qtyProblem('Số cần', 1.0005)).toBe('Số cần tối đa 3 số lẻ.');
  });
});

describe('deliveryCountProblem', () => {
  it.each([1, 2, 99])('%s hợp lệ', (v) => {
    expect(deliveryCountProblem(v)).toBeNull();
  });

  it('chưa nhập thì không báo', () => {
    expect(deliveryCountProblem(null)).toBeNull();
  });

  it.each([0, -1, 100, 32767, 1.5])('%s bị báo ngoài khoảng', (v) => {
    expect(deliveryCountProblem(v)).toBe('Số lần giao dự kiến phải là số nguyên từ 1 đến 99.');
  });
});

describe('noteProblem', () => {
  it('đúng 500 ký tự thì qua, 501 thì báo; tính sau khi cắt khoảng trắng', () => {
    expect(noteProblem('n'.repeat(500))).toBeNull();
    expect(noteProblem(` ${'n'.repeat(500)} `)).toBeNull();
    expect(noteProblem('n'.repeat(501))).toBe('Ghi chú tối đa 500 ký tự.');
    expect(noteProblem('   ')).toBeNull();
    expect(noteProblem(undefined)).toBeNull();
  });
});
