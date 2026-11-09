import { Button, Card, Checkbox, Flex, Input, Segmented, Space } from 'antd';
import { ReloadOutlined, SearchOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { ATTENDANCE_TEXT, MAX_RANGE_DAYS } from '../constants';
import { type DateRange } from '../utils/timesheet';
import { AppDateRangeFields } from '@/components/common/DateInputs';

type Preset = 'today' | 'week' | 'month';

const PRESET_RANGE: Record<Preset, () => DateRange> = {
  today: () => [dayjs().startOf('day'), dayjs().startOf('day')],
  week: () => [dayjs().startOf('week'), dayjs().endOf('week')],
  month: () => [dayjs().startOf('month'), dayjs().endOf('month')],
};

type Props = {
  range: DateRange;
  keyword: string;
  onlyMissingPunch: boolean;
  onRangeChange: (range: DateRange) => void;
  onKeywordChange: (keyword: string) => void;
  onOnlyMissingChange: (value: boolean) => void;
  onReset: () => void;
  /** Màn "của tôi" không cần ô tìm người, chỉ cần chọn kỳ. */
  showKeyword?: boolean;
};

export function TimesheetFilterBar({
  range,
  keyword,
  onlyMissingPunch,
  onRangeChange,
  onKeywordChange,
  onOnlyMissingChange,
  onReset,
  showKeyword = true,
}: Props) {
  // BE từ chối khoảng rộng hơn MAX_RANGE_DAYS, nên khóa sẵn ở lịch thay vì để người dùng chọn rồi báo lỗi.
  const disabledDate = (current: Dayjs, { other }: { other: Dayjs | null }): boolean => {
    if (current.isAfter(dayjs().endOf('day'))) return true;
    return other ? Math.abs(current.diff(other, 'day')) >= MAX_RANGE_DAYS : false;
  };

  const applyPreset = (preset: Preset): void => onRangeChange(PRESET_RANGE[preset]());

  const activePreset = (Object.keys(PRESET_RANGE) as Preset[]).find((key) => {
    const [from, to] = PRESET_RANGE[key]();
    return range[0].isSame(from, 'day') && range[1].isSame(to, 'day');
  });

  return (
    <Card size="small">
      <Flex wrap gap={12} align="center">
        <Segmented
          value={activePreset ?? ''}
          onChange={(value) => applyPreset(value as Preset)}
          options={[
            { label: ATTENDANCE_TEXT.filter.today, value: 'today' },
            { label: ATTENDANCE_TEXT.filter.thisWeek, value: 'week' },
            { label: ATTENDANCE_TEXT.filter.thisMonth, value: 'month' },
          ]}
        />

        <AppDateRangeFields
          value={range}
          allowClear={false}
          disabledDate={disabledDate}
          onChange={(value) => {
            if (value?.[0] && value[1]) onRangeChange([value[0], value[1]]);
          }}
        />

        {showKeyword ? (
          <Input
            allowClear
            prefix={<SearchOutlined />}
            placeholder={ATTENDANCE_TEXT.filter.keyword}
            value={keyword}
            onChange={(e) => onKeywordChange(e.target.value)}
            style={{ minWidth: 240, flex: '1 1 240px' }}
          />
        ) : null}

        <Space size={12} wrap>
          <Checkbox
            checked={onlyMissingPunch}
            onChange={(e) => onOnlyMissingChange(e.target.checked)}
          >
            {ATTENDANCE_TEXT.filter.onlyMissing}
          </Checkbox>
          <Button icon={<ReloadOutlined />} onClick={onReset}>
            {ATTENDANCE_TEXT.filter.reset}
          </Button>
        </Space>
      </Flex>
    </Card>
  );
}
