import { remainingLabel } from './remaining';

describe('remainingLabel', () => {
  it('chưa chốt giá trị', () => {
    expect(remainingLabel(null)).toBe('Chưa chốt');
  });

  it('dương: khách còn phải trả', () => {
    expect(remainingLabel(600_000)).toContain('Khách còn phải trả');
  });

  it('âm: mình còn phải trả khách, hiện số dương', () => {
    const label = remainingLabel(-600_000);
    expect(label).toContain('Mình còn phải trả khách');
    expect(label).not.toContain('-');
  });

  it('bằng 0: đã tất toán', () => {
    expect(remainingLabel(0)).toBe('Đã tất toán');
  });
});
