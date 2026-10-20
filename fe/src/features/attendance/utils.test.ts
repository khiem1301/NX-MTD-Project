import dayjs from 'dayjs';
import {
  defaultRange,
  formatDate,
  formatDateTime,
  formatHours,
  formatNumber,
  formatTime,
  toApiDate,
  workdayTone,
} from './utils';

describe('toApiDate', () => {
  it('trả về yyyy-MM-dd đúng ngày địa phương', () => {
    expect(toApiDate(dayjs('2026-10-20T23:30:00'))).toBe('2026-10-20');
  });
});

describe('format ngày giờ', () => {
  it('hiển thị ngày theo dd/MM/yyyy', () => {
    expect(formatDate('2026-10-20T07:25:00')).toBe('20/10/2026');
  });

  it('hiển thị giờ theo HH:mm', () => {
    expect(formatTime('2026-10-20T07:25:00')).toBe('07:25');
  });

  it('hiển thị ngày giờ đầy đủ', () => {
    expect(formatDateTime('2026-10-20T16:45:00')).toBe('20/10/2026 16:45');
  });

  it('trả về gạch ngang khi giá trị rỗng', () => {
    expect(formatDate(null)).toBe('—');
    expect(formatTime(null)).toBe('—');
    expect(formatDateTime(null)).toBe('—');
  });
});

describe('formatNumber', () => {
  it('bỏ số 0 thừa', () => {
    expect(formatNumber(1)).toBe('1');
    expect(formatNumber(0.5)).toBe('0,5');
  });

  it('làm tròn theo số chữ số yêu cầu', () => {
    expect(formatNumber(1.237)).toBe('1,24');
    expect(formatNumber(12.6, 0)).toBe('13');
  });
});

describe('formatHours', () => {
  it('gắn hậu tố h cho số dương', () => {
    expect(formatHours(7.5)).toBe('7,5h');
  });

  it('không hiện 0h mà hiện gạch ngang', () => {
    expect(formatHours(0)).toBe('—');
  });
});

describe('workdayTone', () => {
  it('đủ công trở lên là full', () => {
    expect(workdayTone(1)).toBe('full');
    expect(workdayTone(1.5)).toBe('full');
  });

  it('công lẻ là partial', () => {
    expect(workdayTone(0.5)).toBe('partial');
  });

  it('không công là none', () => {
    expect(workdayTone(0)).toBe('none');
  });
});

describe('defaultRange', () => {
  it('mặc định là trọn tháng hiện tại', () => {
    const [from, to] = defaultRange();
    expect(from.isSame(dayjs().startOf('month'), 'day')).toBe(true);
    expect(to.isSame(dayjs().endOf('month'), 'day')).toBe(true);
  });
});
