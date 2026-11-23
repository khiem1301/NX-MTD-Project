import { useMemo, useState } from 'react';
import { Alert, Button, Space, Typography } from 'antd';
import { CalculatorOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { ERROR_CODES } from '@/constants/errorCodes';
import { MESSAGES } from '@/constants/messages';
import type { ApiError } from '@/types/api';
import { API_MONTH, toApiMonth } from '@/utils/format';
import { PageCard } from '@/components/common/PageCard';
import { AppMonthPicker } from '@/components/common/DateInputs';
import { BreakEvenCalculateModal } from '../components/BreakEvenCalculateModal';
import { BreakEvenResultTable } from '../components/BreakEvenResultTable';
import { BreakEvenSummaryCard } from '../components/BreakEvenSummaryCard';
import { BREAK_EVEN_TEXT } from '../constants';
import { useBreakEven } from '../hooks/useBreakEven';
import type { BreakEvenFilter } from '../types';
import { pivotResults, summarize } from '../utils/pivotResults';

/**
 * UC11 — Admin: chọn tháng, mở popup "Tính hòa vốn" (xem dữ liệu đầu vào rồi tính cả hai cách chia định phí), xem / so sánh /
 * xuất kết quả (mật, chỉ Admin — GBR-02; DEV-ALL-169).
 */
export function BreakEvenPage() {
  const [filter, setFilter] = useState<BreakEvenFilter>(() => ({
    periodMonth: toApiMonth(dayjs()),
    history: false,
  }));
  const [calculating, setCalculating] = useState(false);

  const breakEven = useBreakEven(filter);
  const error = breakEven.error as ApiError | null;
  const notFound = error?.errorCode === ERROR_CODES.BreakEvenNotFound;
  const notReady = error?.errorCode === ERROR_CODES.BreakEvenNotReady;
  const rows = useMemo(() => pivotResults(breakEven.data?.items ?? []), [breakEven.data]);
  const summary = useMemo(() => summarize(rows), [rows]);

  return (
    <PageCard title={BREAK_EVEN_TEXT.pageTitle}>
      <Space wrap>
        <Typography.Text>{BREAK_EVEN_TEXT.periodMonth}</Typography.Text>
        <AppMonthPicker
          allowClear={false}
          value={dayjs(filter.periodMonth, API_MONTH)}
          onChange={(value: Dayjs | null) => {
            if (value) setFilter({ ...filter, periodMonth: toApiMonth(value) });
          }}
        />
        <Button type="primary" icon={<CalculatorOutlined />} onClick={() => setCalculating(true)}>
          {BREAK_EVEN_TEXT.openCalculate}
        </Button>
      </Space>

      {error && !notFound ? (
        <Alert type={notReady ? 'warning' : 'error'} showIcon message={error.message} />
      ) : null}
      {!breakEven.isLoading && (notFound || !breakEven.data) ? (
        <Alert type="info" showIcon message={MESSAGES.breakEven.noDataYet} />
      ) : null}

      {summary && !filter.history && <BreakEvenSummaryCard summary={summary} />}
      {breakEven.data ? (
        <BreakEvenResultTable rows={rows} filter={filter} onFilterChange={setFilter} />
      ) : null}

      <BreakEvenCalculateModal
        periodMonth={filter.periodMonth}
        open={calculating}
        onClose={() => setCalculating(false)}
      />
    </PageCard>
  );
}
