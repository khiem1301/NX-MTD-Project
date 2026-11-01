import { useState } from 'react';
import { Alert, Space, Typography } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { ERROR_CODES } from '@/constants/errorCodes';
import { MESSAGES } from '@/constants/messages';
import type { ApiError } from '@/types/api';
import { CostSummaryPanel } from '../components/CostSummaryPanel';
import { COST_SUMMARY_TEXT } from '../constants';
import { useCostSummary } from '../hooks/useCostSummary';
import type { CostSummaryFilter } from '../types';
import { PERIOD_MONTH_FORMAT, currentPeriodMonth, toPeriodMonth } from '../utils';
import { PageCard } from '@/components/common/PageCard';
import { AppMonthPicker } from '@/components/common/DateInputs';

/** UC12 — Admin: chọn tháng, xem tổng hợp chi phí (vật tư + lương), xuất Excel. Dữ liệu "mật" (GBR-02). */
export function CostSummaryPage() {
  const [filter, setFilter] = useState<CostSummaryFilter>(() => ({
    periodMonth: toPeriodMonth(currentPeriodMonth()),
  }));

  const costSummary = useCostSummary(filter);
  const error = costSummary.error as ApiError | null;
  const notFound = error?.errorCode === ERROR_CODES.MonthlyCostNotFound;

  return (
    <PageCard title={COST_SUMMARY_TEXT.pageTitle} description={COST_SUMMARY_TEXT.pageSubtitle}>
      <Space>
        <Typography.Text>{COST_SUMMARY_TEXT.periodMonth}</Typography.Text>
        <AppMonthPicker
          allowClear={false}
          value={dayjs(filter.periodMonth, PERIOD_MONTH_FORMAT)}
          onChange={(value: Dayjs | null) => {
            if (value) setFilter({ periodMonth: toPeriodMonth(value) });
          }}
        />
      </Space>

      {error && !notFound ? <Alert type="error" showIcon message={error.message} /> : null}
      {!costSummary.isLoading && (notFound || !costSummary.data) ? (
        <Alert type="info" showIcon message={MESSAGES.costSummary.noDataYet} />
      ) : null}

      {costSummary.data ? <CostSummaryPanel summary={costSummary.data} filter={filter} /> : null}
    </PageCard>
  );
}
