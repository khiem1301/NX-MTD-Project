import { useState } from 'react';
import { Alert, Button, Space, Typography } from 'antd';
import { UploadOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { ERROR_CODES } from '@/constants/errorCodes';
import type { ApiError } from '@/types/api';
import { PayrollExportPanel } from '../components/PayrollExportPanel';
import { PayrollImportModal } from '../components/PayrollImportModal';
import { PayrollSummaryPanel } from '../components/PayrollSummaryPanel';
import { PAYROLL_TEXT } from '../constants';
import { usePayrollByMonth } from '../hooks/usePayroll';
import { API_MONTH, toApiMonth } from '@/utils/format';
import { PageCard } from '@/components/common/PageCard';
import { AppMonthPicker } from '@/components/common/DateInputs';

/** UC31 — Kế toán: xuất dữ liệu tính lương theo tháng, nhập bảng lương đã tính từ Excel (popup), gửi duyệt. */
export function AccountantPayrollPage() {
  const [periodMonth, setPeriodMonth] = useState(() => toApiMonth(dayjs()));
  const [importOpen, setImportOpen] = useState(false);
  const payroll = usePayrollByMonth(periodMonth);
  const error = payroll.error as ApiError | null;
  const notFound = error?.errorCode === ERROR_CODES.PayrollNotFound;
  const canImport =
    !payroll.isLoading &&
    (notFound || payroll.data?.status === 'Draft' || payroll.data?.status === 'Rejected');

  return (
    <PageCard
      title={PAYROLL_TEXT.accountantPageTitle}
      extra={
        <Button
          type="primary"
          icon={<UploadOutlined />}
          disabled={!canImport}
          onClick={() => setImportOpen(true)}
        >
          {PAYROLL_TEXT.openImport}
        </Button>
      }
    >
      <Space>
        <Typography.Text>{PAYROLL_TEXT.periodMonth}</Typography.Text>
        <AppMonthPicker
          allowClear={false}
          value={dayjs(periodMonth, API_MONTH)}
          onChange={(value: Dayjs | null) => {
            if (value) setPeriodMonth(toApiMonth(value));
          }}
        />
      </Space>

      {!payroll.isLoading && !canImport ? (
        <Alert type="warning" showIcon message={PAYROLL_TEXT.importDisabledHint} />
      ) : null}
      {error && !notFound ? <Alert type="error" showIcon message={error.message} /> : null}

      <PayrollExportPanel periodMonth={periodMonth} />

      {!payroll.isLoading && (notFound || !payroll.data) ? (
        <Alert type="info" showIcon message={PAYROLL_TEXT.noPayrollYet} />
      ) : null}
      {payroll.data ? (
        <PayrollSummaryPanel periodMonth={periodMonth} summary={payroll.data} />
      ) : null}

      {importOpen && (
        <PayrollImportModal open periodMonth={periodMonth} onClose={() => setImportOpen(false)} />
      )}
    </PageCard>
  );
}
