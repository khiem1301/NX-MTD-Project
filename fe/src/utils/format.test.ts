import type { Dayjs } from 'dayjs';
import { fill, formatCurrency, formatDate, formatDateTime, formatQty, toApiDate } from './format';

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

describe('formatCurrency', () => {
  it('ngăn nghìn bằng dấu chấm và thêm ₫', () => {
    expect(formatCurrency(1250000)).toBe('1.250.000 ₫');
  });

  it('trả gạch ngang khi không có giá trị', () => {
    expect(formatCurrency(null)).toBe('—');
  });
});

describe('formatDateTime với múi giờ', () => {
  it('đổi chuỗi UTC về giờ Việt Nam', () => {
    expect(formatDateTime('2026-10-22T01:30:00Z')).toBe('22/10/2026 08:30');
  });

  it('giữ nguyên chuỗi đã có offset +07:00', () => {
    expect(formatDateTime('2026-10-22T08:30:00+07:00')).toBe('22/10/2026 08:30');
  });
});

describe('toApiDate / fill', () => {
  it('toApiDate trả YYYY-MM-DD', () => {
    expect(toApiDate({ format: () => '2026-10-22' } as unknown as Dayjs)).toBe('2026-10-22');
  });

  it('fill thay {0}, {1}', () => {
    expect(fill('Đã đủ {0} ảnh ({1})', 5, 'tối đa')).toBe('Đã đủ 5 ảnh (tối đa)');
  });
});
