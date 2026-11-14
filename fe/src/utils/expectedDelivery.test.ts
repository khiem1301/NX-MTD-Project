import dayjs from 'dayjs';
import {
  deliveryDateSlots,
  formatDeliveryDates,
  lastDeliveryDate,
  toDeliveryDateValues,
  toDeliveryDatesPayload,
} from './expectedDelivery';

describe('expectedDelivery', () => {
  it('số ô ngày bằng số lần giao, kẹp trong 0..99', () => {
    expect(deliveryDateSlots(3)).toBe(3);
    expect(deliveryDateSlots(null)).toBe(0);
    expect(deliveryDateSlots(500)).toBe(99);
  });

  it('gửi đúng số ngày theo số lần giao, bỏ ô trống', () => {
    const dates = [dayjs('2026-12-10'), dayjs('2026-12-15'), dayjs('2026-12-20')];
    expect(toDeliveryDatesPayload(2, dates)).toEqual(['2026-12-10', '2026-12-15']);
    expect(toDeliveryDatesPayload(3, [dates[0], undefined, dates[2]])).toEqual([
      '2026-12-10',
      '2026-12-20',
    ]);
    expect(toDeliveryDatesPayload(1, undefined)).toEqual([]);
  });

  it('điền sẵn từ danh sách, dữ liệu cũ thì lấy ngày đầu / cuối', () => {
    expect(
      toDeliveryDateValues(['2026-12-10', '2026-12-12']).map((d) => d.format('YYYY-MM-DD')),
    ).toEqual(['2026-12-10', '2026-12-12']);
    expect(
      toDeliveryDateValues([], '2026-12-10', '2026-12-20').map((d) => d.format('YYYY-MM-DD')),
    ).toEqual(['2026-12-10', '2026-12-20']);
  });

  it('hiển thị các ngày cách nhau dấu phẩy, thiếu danh sách thì từ – đến', () => {
    expect(formatDeliveryDates(['2026-12-10', '2026-12-15'])).toBe('10/12/2026, 15/12/2026');
    expect(formatDeliveryDates([], '2026-12-10', null)).toBe('10/12/2026');
    expect(formatDeliveryDates(undefined, '2026-12-10', '2026-12-20')).toBe(
      '10/12/2026 – 20/12/2026',
    );
  });

  it('ngày giao cuối', () => {
    expect(lastDeliveryDate(['2026-12-10', '2026-12-15'], '2026-12-10', '2026-12-30')).toBe(
      '2026-12-15',
    );
    expect(lastDeliveryDate([], '2026-12-10', '2026-12-30')).toBe('2026-12-30');
    expect(lastDeliveryDate(null, '2026-12-10', null)).toBe('2026-12-10');
    expect(lastDeliveryDate()).toBeNull();
  });
});
