import { useState } from 'react';
import { Alert, DatePicker, Space, Typography } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { ERROR_CODES } from '@/constants/errorCodes';
import { MESSAGES } from '@/constants/messages';
import type { ApiError } from '@/types/api';
import { BreakEvenCalculatePanel } from '../components/BreakEvenCalculatePanel';
import { BreakEvenResultTable } from '../components/BreakEvenResultTable';
import { BREAK_EVEN_TEXT } from '../constants';
import { useBreakEven } from '../hooks/useBreakEven';
import type { BreakEvenFilter } from '../types';
import { PERIOD_MONTH_FORMAT, currentPeriodMonth, toPeriodMonth } from '../utils';

/** UC11 — Admin: chọn tháng + cách phân bổ, chạy tính hòa vốn, xem/xuất kết quả (mật, chỉ Admin — GBR-02). */
export function BreakEvenPage() {
  const [filter, setFilter] = useState<BreakEvenFilter>(() => ({
    periodMonth: toPeriodMonth(currentPeriodMonth()),
    history: false,
  }));

  const breakEven = useBreakEven(filter);
  const error = breakEven.error as ApiError | null;
  const notFound = error?.errorCode === ERROR_CODES.BreakEvenNotFound;
  const notReady = error?.errorCode === ERROR_CODES.BreakEvenNotReady;

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div>
        <Typography.Title level={3} style={{ marginBottom: 4 }}>
          {BREAK_EVEN_TEXT.pageTitle}
        </Typography.Title>
        <Typography.Text type="secondary">{BREAK_EVEN_TEXT.pageSubtitle}</Typography.Text>
      </div>

      <Space>
        <Typography.Text>{BREAK_EVEN_TEXT.periodMonth}</Typography.Text>
        <DatePicker
          picker="month"
          format={PERIOD_MONTH_FORMAT}
          allowClear={false}
          value={dayjs(filter.periodMonth, PERIOD_MONTH_FORMAT)}
          onChange={(value: Dayjs | null) => {
            if (value) setFilter({ ...filter, periodMonth: toPeriodMonth(value) });
          }}
        />
      </Space>

      <BreakEvenCalculatePanel periodMonth={filter.periodMonth} />

      {error && !notFound ? (
        <Alert type={notReady ? 'warning' : 'error'} showIcon message={error.message} />
      ) : null}
      {!breakEven.isLoading && (notFound || !breakEven.data) ? (
        <Alert type="info" showIcon message={MESSAGES.breakEven.noDataYet} />
      ) : null}

      {breakEven.data ? (
        <BreakEvenResultTable items={breakEven.data.items} filter={filter} onFilterChange={setFilter} />
      ) : null}
    </Space>
  );
}
