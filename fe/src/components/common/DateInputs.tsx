import { DatePicker, TimePicker } from 'antd';
import type { DatePickerProps, GetProps } from 'antd';
import dayjs from 'dayjs';
import { DATE_FORMAT, MONTH_FORMAT, TIME_FORMAT } from '@/utils/format';

type RangeProps = GetProps<typeof DatePicker.RangePicker>;
type TimeProps = GetProps<typeof TimePicker>;

/** Ô chọn ngày chuẩn: dd/MM/yyyy, cùng độ rộng và placeholder ở mọi màn hình. */
export function AppDatePicker(props: Omit<DatePickerProps, 'picker' | 'format'>) {
  return (
    <DatePicker format={DATE_FORMAT} placeholder="dd/mm/yyyy" style={{ width: 160 }} {...props} />
  );
}

/** Ô chọn tháng chuẩn: MM/yyyy. */
export function AppMonthPicker(props: Omit<DatePickerProps, 'picker' | 'format'>) {
  return (
    <DatePicker
      picker="month"
      format={MONTH_FORMAT}
      placeholder="mm/yyyy"
      style={{ width: 130 }}
      {...props}
    />
  );
}

/** Chọn nhanh cho ô khoảng ngày. */
const rangePresets = (): RangeProps['presets'] => [
  { label: 'Hôm nay', value: [dayjs(), dayjs()] },
  { label: '7 ngày qua', value: [dayjs().subtract(6, 'day'), dayjs()] },
  { label: 'Tháng này', value: [dayjs().startOf('month'), dayjs().endOf('month')] },
  {
    label: 'Tháng trước',
    value: [
      dayjs().subtract(1, 'month').startOf('month'),
      dayjs().subtract(1, 'month').endOf('month'),
    ],
  },
];

/** Ô chọn khoảng ngày chuẩn: dd/MM/yyyy, có chọn nhanh. */
export function AppRangePicker(props: Omit<RangeProps, 'format'>) {
  return (
    <DatePicker.RangePicker
      format={DATE_FORMAT}
      placeholder={['Từ ngày', 'Đến ngày']}
      presets={rangePresets()}
      style={{ width: 280 }}
      {...props}
    />
  );
}

/** Ô chọn giờ chuẩn: HH:mm. */
export function AppTimePicker(props: Omit<TimeProps, 'format'>) {
  return <TimePicker format={TIME_FORMAT} placeholder="hh:mm" style={{ width: 120 }} {...props} />;
}
