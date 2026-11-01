import { useMemo, useState, type ReactNode } from 'react';
import { Button, Calendar, Flex, Tag, Typography, theme } from 'antd';
import { LeftOutlined, RightOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { API_DATE, fill } from '@/utils/format';
import { KPI_TEXT } from '../constants';
import { monthsOfPeriod } from '../utils/monthsOfPeriod';
import './KpiMonthCalendar.css';

type KpiMonthCalendarProps = {
  startDate: string;
  endDate: string;
  today: string;
  dueDate: string;
  /** Nội dung một ô ngày; null là ngày ngoài kỳ KPI. */
  renderDay: (date: string) => ReactNode | null;
};

const SUNDAY = 0;
const text = KPI_TEXT.calendar;

/** Lịch một tháng của kỳ KPI; chỉ chuyển được giữa các tháng trong kỳ. Ô sau ngày giao hàng tô nền cảnh báo. */
export function KpiMonthCalendar({
  startDate,
  endDate,
  today,
  dueDate,
  renderDay,
}: KpiMonthCalendarProps) {
  const { token } = theme.useToken();
  const months = useMemo(() => monthsOfPeriod(startDate, endDate), [startDate, endDate]);
  const [index, setIndex] = useState(() =>
    Math.max(0, months.indexOf(dayjs(today).startOf('month').format(API_DATE))),
  );
  const month = dayjs(months[index] ?? startDate);

  return (
    <Calendar
      className="kpi-month-calendar"
      value={month}
      mode="month"
      headerRender={() => (
        <Flex justify="center" align="center" gap={8} style={{ padding: '4px 0' }}>
          <Button
            size="small"
            icon={<LeftOutlined />}
            aria-label={text.prevMonth}
            disabled={index === 0}
            onClick={() => setIndex(index - 1)}
          />
          <Typography.Text strong>
            {fill(text.monthTitle, month.month() + 1, month.year())}
          </Typography.Text>
          <Button
            size="small"
            icon={<RightOutlined />}
            aria-label={text.nextMonth}
            disabled={index >= months.length - 1}
            onClick={() => setIndex(index + 1)}
          />
        </Flex>
      )}
      fullCellRender={(date, info) => {
        if (info.type !== 'date') return info.originNode;
        const key = date.format(API_DATE);
        const content = date.isSame(month, 'month') ? renderDay(key) : null;
        const inPeriodBackground = key > dueDate ? token.colorWarningBg : undefined;
        return (
          <Flex
            vertical
            gap={2}
            style={{
              minHeight: 64,
              margin: '0 1px',
              padding: '2px 4px',
              textAlign: 'left',
              borderTop: `2px solid ${key === today ? token.colorPrimary : token.colorBorderSecondary}`,
              background: content === null ? token.colorFillQuaternary : inPeriodBackground,
            }}
          >
            <Flex justify="space-between" align="center">
              <Typography.Text
                strong
                type={content === null ? 'secondary' : undefined}
                style={date.day() === SUNDAY ? { color: token.colorWarningText } : undefined}
              >
                {date.date()}
              </Typography.Text>
              {content !== null && key === dueDate && (
                <Tag color="gold" style={{ margin: 0 }}>
                  {text.dueDay}
                </Tag>
              )}
            </Flex>
            {content}
          </Flex>
        );
      }}
    />
  );
}
