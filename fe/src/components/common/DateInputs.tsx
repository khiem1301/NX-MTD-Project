import { useState } from 'react';
import { DatePicker, Space, TimePicker } from 'antd';
import type { DatePickerProps, GetProps } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { DATE_FORMAT, MONTH_FORMAT, TIME_FORMAT } from '@/utils/format';

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

export type DateRangeValue = [Dayjs | null, Dayjs | null];

type DateRangeFieldsProps = {
  value?: DateRangeValue | null;
  /** Mặc định chỉ báo khi đã chọn đủ hai đầu (hoặc xóa hết); `allowPartial` báo cả khi mới chọn một đầu. */
  onChange?: (range: DateRangeValue | null) => void;
  /** `other` là ngày ở ô còn lại (null nếu chưa chọn), `position` cho biết đang vẽ ô nào. */
  disabledDate?: (date: Dayjs, info: { other: Dayjs | null; position: 'from' | 'to' }) => boolean;
  allowClear?: boolean;
  allowPartial?: boolean;
  disabled?: boolean;
  status?: DatePickerProps['status'];
  fromPlaceholder?: string;
  toPlaceholder?: string;
};

/**
 * Khoảng ngày nhập bằng hai ô riêng "Từ ngày" và "Đến ngày": ô sau không chọn được ngày trước ô đầu; đổi ô đầu sang sau
 * ô sau thì ô sau kéo theo. Dùng được trực tiếp trong Form.Item (value / onChange).
 */
export function AppDateRangeFields({
  value,
  onChange,
  disabledDate,
  allowClear = true,
  allowPartial = false,
  disabled,
  status,
  fromPlaceholder = 'Từ ngày',
  toPlaceholder = 'Đến ngày',
}: DateRangeFieldsProps) {
  const fromMs = value?.[0]?.valueOf() ?? null;
  const toMs = value?.[1]?.valueOf() ?? null;
  const [draft, setDraft] = useState<DateRangeValue>([
    fromMs === null ? null : dayjs(fromMs),
    toMs === null ? null : dayjs(toMs),
  ]);

  // Giá trị từ ngoài đổi (đặt lại bộ lọc, chọn nhanh...) thì ô theo đó; ô mới chọn một đầu thì giữ nguyên.
  const [syncedKey, setSyncedKey] = useState(`${fromMs}|${toMs}`);
  if (syncedKey !== `${fromMs}|${toMs}`) {
    setSyncedKey(`${fromMs}|${toMs}`);
    setDraft([fromMs === null ? null : dayjs(fromMs), toMs === null ? null : dayjs(toMs)]);
  }

  const commit = (next: DateRangeValue) => {
    setDraft(next);
    if (next[0] && next[1]) onChange?.(next);
    else if (!next[0] && !next[1]) onChange?.(null);
    else if (allowPartial) onChange?.(next);
  };

  const [from, to] = draft;
  return (
    <Space size={8} wrap>
      <AppDatePicker
        aria-label={fromPlaceholder}
        placeholder={fromPlaceholder}
        allowClear={allowClear}
        disabled={disabled}
        status={status}
        value={from}
        disabledDate={(d) =>
          (disabledDate?.(d, { other: to, position: 'from' }) ?? false) ||
          (allowPartial && to ? d.isAfter(to, 'day') : false)
        }
        onChange={(d) => commit([d, d && to && d.isAfter(to, 'day') ? d : to])}
      />
      <AppDatePicker
        aria-label={toPlaceholder}
        placeholder={toPlaceholder}
        allowClear={allowClear}
        disabled={disabled}
        status={status}
        value={to}
        disabledDate={(d) =>
          (disabledDate?.(d, { other: from, position: 'to' }) ?? false) ||
          (from ? d.isBefore(from, 'day') : false)
        }
        onChange={(d) => commit([from, d])}
      />
    </Space>
  );
}

/** Ô chọn giờ chuẩn: HH:mm. */
export function AppTimePicker(props: Omit<TimeProps, 'format'>) {
  return <TimePicker format={TIME_FORMAT} placeholder="hh:mm" style={{ width: 120 }} {...props} />;
}
