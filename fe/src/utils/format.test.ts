import { formatDate, formatDateTime, formatQty } from './format';

describe('formatQty', () => {
  it('dùng dấu chấm ngăn nghìn, dấu phẩy thập phân', () => {
    expect(formatQty(1250.5)).toBe('1.250,5');
  });

  it('làm tròn tối đa 3 chữ số thập phân', () => {
    expect(formatQty(2.40312)).toBe('2,403');
  });

  it('trả gạch ngang khi không có giá trị', () => {
    expect(formatQty(null)).toBe('—');
    expect(formatQty(undefined)).toBe('—');
  });

  it('hiển thị số 0', () => {
    expect(formatQty(0)).toBe('0');
  });
});

describe('formatDate', () => {
  it('đổi yyyy-MM-dd sang dd/MM/yyyy', () => {
    expect(formatDate('2026-10-05')).toBe('05/10/2026');
  });

  it('bỏ phần giờ nếu có', () => {
    expect(formatDate('2026-10-05T23:59:00')).toBe('05/10/2026');
  });

  it('trả gạch ngang khi rỗng', () => {
    expect(formatDate(null)).toBe('—');
  });
});

describe('formatDateTime', () => {
  it('đổi sang dd/MM/yyyy HH:mm', () => {
    expect(formatDateTime('2026-10-20T08:05:33.123')).toBe('20/10/2026 08:05');
  });

  it('chỉ có ngày thì trả ngày', () => {
    expect(formatDateTime('2026-10-20')).toBe('20/10/2026');
  });
});
