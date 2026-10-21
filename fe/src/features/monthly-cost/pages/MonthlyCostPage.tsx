import { useState } from 'react';
import { Alert, DatePicker, Space, Typography } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { ERROR_CODES } from '@/constants/errorCodes';
import { MESSAGES } from '@/constants/messages';
import type { ApiError } from '@/types/api';
import { MonthlyCostImportPanel } from '../components/MonthlyCostImportPanel';
import { MonthlyCostSummaryPanel } from '../components/MonthlyCostSummaryPanel';
import { MONTHLY_COST_TEXT } from '../constants';
import { useMonthlyCostByMonth } from '../hooks/useMonthlyCost';
import { PERIOD_MONTH_FORMAT, currentPeriodMonth, toPeriodMonth } from '../utils';

/** UC12/UC33 — Kế toán: tải file mẫu, xem trước rồi nhập chi phí tháng từ Excel; xem chi phí đã nhập. */
export function MonthlyCostPage() {
  const [periodMonth, setPeriodMonth] = useState(() => toPeriodMonth(currentPeriodMonth()));
  const monthlyCost = useMonthlyCostByMonth(periodMonth);
  const error = monthlyCost.error as ApiError | null;
  const notFound = error?.errorCode === ERROR_CODES.MonthlyCostNotFound;
  const canImport = !monthlyCost.isLoading && (notFound || monthlyCost.data?.isLocked === false);

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div>
        <Typography.Title level={3} style={{ marginBottom: 4 }}>
          {MONTHLY_COST_TEXT.pageTitle}
        </Typography.Title>
        <Typography.Text type="secondary">{MONTHLY_COST_TEXT.pageSubtitle}</Typography.Text>
      </div>

      <Space>
        <Typography.Text>{MONTHLY_COST_TEXT.periodMonth}</Typography.Text>
        <DatePicker
          picker="month"
          format={PERIOD_MONTH_FORMAT}
          allowClear={false}
          value={dayjs(periodMonth, PERIOD_MONTH_FORMAT)}
          onChange={(value: Dayjs | null) => {
            if (value) setPeriodMonth(toPeriodMonth(value));
          }}
        />
      </Space>

      {error && !notFound ? <Alert type="error" showIcon message={error.message} /> : null}

      {!monthlyCost.isLoading && (notFound || !monthlyCost.data) ? (
        <Alert type="info" showIcon message={MESSAGES.monthlyCost.noDataYet} />
      ) : null}
      {monthlyCost.data ? <MonthlyCostSummaryPanel summary={monthlyCost.data} /> : null}

      <MonthlyCostImportPanel periodMonth={periodMonth} disabled={!canImport} />
    </Space>
  );
}
