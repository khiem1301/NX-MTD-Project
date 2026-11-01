import { useState } from 'react';
import { Alert, Space, Typography } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { ERROR_CODES } from '@/constants/errorCodes';
import { MESSAGES } from '@/constants/messages';
import type { ApiError } from '@/types/api';
import { MonthlyCostImportPanel } from '../components/MonthlyCostImportPanel';
import { MonthlyCostSummaryPanel } from '../components/MonthlyCostSummaryPanel';
import { MONTHLY_COST_TEXT } from '../constants';
import { useMonthlyCostByMonth } from '../hooks/useMonthlyCost';
import { PERIOD_MONTH_FORMAT, currentPeriodMonth, toPeriodMonth } from '../utils';
import { PageCard } from '@/components/common/PageCard';
import { AppMonthPicker } from '@/components/common/DateInputs';

/** UC12/UC33 — Kế toán: tải file mẫu, xem trước rồi nhập chi phí tháng từ Excel; xem chi phí đã nhập. */
export function MonthlyCostPage() {
  const [periodMonth, setPeriodMonth] = useState(() => toPeriodMonth(currentPeriodMonth()));
  const monthlyCost = useMonthlyCostByMonth(periodMonth);
  const error = monthlyCost.error as ApiError | null;
  const notFound = error?.errorCode === ERROR_CODES.MonthlyCostNotFound;
  const canImport = !monthlyCost.isLoading && (notFound || monthlyCost.data?.isLocked === false);

  return (
    <PageCard title={MONTHLY_COST_TEXT.pageTitle} description={MONTHLY_COST_TEXT.pageSubtitle}>
      <Space>
        <Typography.Text>{MONTHLY_COST_TEXT.periodMonth}</Typography.Text>
        <AppMonthPicker
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
    </PageCard>
  );
}
