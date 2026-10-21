import { useState } from 'react';
import { Alert, DatePicker, Space, Typography } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { ERROR_CODES } from '@/constants/errorCodes';
import { MESSAGES } from '@/constants/messages';
import type { ApiError } from '@/types/api';
import { CostSummaryPanel } from '../components/CostSummaryPanel';
import { COST_SUMMARY_TEXT } from '../constants';
import { useCostSummary } from '../hooks/useCostSummary';
import type { CostSummaryFilter } from '../types';
import { PERIOD_MONTH_FORMAT, currentPeriodMonth, toPeriodMonth } from '../utils';

/** UC12 — Admin: chọn tháng, xem tổng hợp chi phí (vật tư + lương), xuất Excel. Dữ liệu "mật" (GBR-02). */
export function CostSummaryPage() {
  const [filter, setFilter] = useState<CostSummaryFilter>(() => ({
    periodMonth: toPeriodMonth(currentPeriodMonth()),
  }));

  const costSummary = useCostSummary(filter);
  const error = costSummary.error as ApiError | null;
  const notFound = error?.errorCode === ERROR_CODES.MonthlyCostNotFound;

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div>
        <Typography.Title level={3} style={{ marginBottom: 4 }}>
          {COST_SUMMARY_TEXT.pageTitle}
        </Typography.Title>
        <Typography.Text type="secondary">{COST_SUMMARY_TEXT.pageSubtitle}</Typography.Text>
      </div>

      <Space>
        <Typography.Text>{COST_SUMMARY_TEXT.periodMonth}</Typography.Text>
        <DatePicker
          picker="month"
          format={PERIOD_MONTH_FORMAT}
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
    </Space>
  );
}
