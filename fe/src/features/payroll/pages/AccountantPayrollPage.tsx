import { useState } from 'react';
import { Alert, DatePicker, Space, Typography } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { ERROR_CODES } from '@/constants/errorCodes';
import type { ApiError } from '@/types/api';
import { PayrollExportPanel } from '../components/PayrollExportPanel';
import { PayrollImportPanel } from '../components/PayrollImportPanel';
import { PayrollSummaryPanel } from '../components/PayrollSummaryPanel';
import { PAYROLL_TEXT } from '../constants';
import { usePayrollByMonth } from '../hooks/usePayroll';
import { PERIOD_MONTH_FORMAT, currentPeriodMonth, toPeriodMonth } from '../utils';

/** UC31 — Kế toán: xuất dữ liệu tính lương theo tháng, nhập bảng lương đã tính từ Excel, gửi duyệt. */
export function AccountantPayrollPage() {
  const [periodMonth, setPeriodMonth] = useState(() => toPeriodMonth(currentPeriodMonth()));
  const payroll = usePayrollByMonth(periodMonth);
  const error = payroll.error as ApiError | null;
  const notFound = error?.errorCode === ERROR_CODES.PayrollNotFound;
  const canImport =
    !payroll.isLoading &&
    (notFound || payroll.data?.status === 'Draft' || payroll.data?.status === 'Rejected');

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div>
        <Typography.Title level={3} style={{ marginBottom: 4 }}>
          {PAYROLL_TEXT.accountantPageTitle}
        </Typography.Title>
        <Typography.Text type="secondary">{PAYROLL_TEXT.accountantPageSubtitle}</Typography.Text>
      </div>

      <Space>
        <Typography.Text>{PAYROLL_TEXT.periodMonth}</Typography.Text>
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

      <PayrollExportPanel periodMonth={periodMonth} />

      {!payroll.isLoading && (notFound || !payroll.data) ? (
        <Alert type="info" showIcon message={PAYROLL_TEXT.noPayrollYet} />
      ) : null}
      {payroll.data ? <PayrollSummaryPanel periodMonth={periodMonth} summary={payroll.data} /> : null}

      <PayrollImportPanel periodMonth={periodMonth} disabled={!canImport} />
    </Space>
  );
}
