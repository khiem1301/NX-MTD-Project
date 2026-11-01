import { useState } from 'react';
import { Alert, Space, Typography } from 'antd';
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
import { PageCard } from '@/components/common/PageCard';
import { AppMonthPicker } from '@/components/common/DateInputs';

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
    <PageCard title={BREAK_EVEN_TEXT.pageTitle} description={BREAK_EVEN_TEXT.pageSubtitle}>
      <Space>
        <Typography.Text>{BREAK_EVEN_TEXT.periodMonth}</Typography.Text>
        <AppMonthPicker
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
        <BreakEvenResultTable
          items={breakEven.data.items}
          filter={filter}
          onFilterChange={setFilter}
        />
      ) : null}
    </PageCard>
  );
}
