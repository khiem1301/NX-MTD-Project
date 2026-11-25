import { useState } from 'react';
import { Alert, Button, Space, Typography } from 'antd';
import { DownloadOutlined, UploadOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { ERROR_CODES } from '@/constants/errorCodes';
import { MESSAGES } from '@/constants/messages';
import type { ApiError } from '@/types/api';
import { MonthlyCostImportModal } from '../components/MonthlyCostImportModal';
import { MonthlyCostSummaryPanel } from '../components/MonthlyCostSummaryPanel';
import { MONTHLY_COST_IMPORT_TEXT, MONTHLY_COST_TEXT } from '../constants';
import { useDownloadMonthlyCostTemplate, useMonthlyCostByMonth } from '../hooks/useMonthlyCost';
import { API_MONTH, toApiMonth } from '@/utils/format';
import { PageCard } from '@/components/common/PageCard';
import { AppMonthPicker } from '@/components/common/DateInputs';

/** UC12/UC33 — Kế toán: nhập chi phí tháng từ Excel ở popup (xem trước, sửa từng ô, kiểm lại); xem chi phí đã nhập. */
export function MonthlyCostPage() {
  const [periodMonth, setPeriodMonth] = useState(() => toApiMonth(dayjs()));
  const [importOpen, setImportOpen] = useState(false);
  const monthlyCost = useMonthlyCostByMonth(periodMonth);
  const downloadTemplate = useDownloadMonthlyCostTemplate();
  const error = monthlyCost.error as ApiError | null;
  const notFound = error?.errorCode === ERROR_CODES.MonthlyCostNotFound;
  const locked = monthlyCost.data?.isLocked === true;
  const canImport = !monthlyCost.isLoading && (notFound || monthlyCost.data?.isLocked === false);

  return (
    <PageCard
      title={MONTHLY_COST_TEXT.pageTitle}
      extra={
        <Space>
          <Button
            icon={<DownloadOutlined />}
            loading={downloadTemplate.isPending}
            onClick={() => downloadTemplate.mutate()}
          >
            {MONTHLY_COST_TEXT.downloadTemplate}
          </Button>
          <Button
            type="primary"
            icon={<UploadOutlined />}
            disabled={!canImport}
            onClick={() => setImportOpen(true)}
          >
            {MONTHLY_COST_IMPORT_TEXT.open}
          </Button>
        </Space>
      }
    >
      <Space>
        <Typography.Text>{MONTHLY_COST_TEXT.periodMonth}</Typography.Text>
        <AppMonthPicker
          allowClear={false}
          value={dayjs(periodMonth, API_MONTH)}
          onChange={(value: Dayjs | null) => {
            if (value) setPeriodMonth(toApiMonth(value));
          }}
        />
      </Space>

      {locked ? (
        <Alert type="warning" showIcon message={MESSAGES.monthlyCost.importDisabledHint} />
      ) : null}
      {error && !notFound ? <Alert type="error" showIcon message={error.message} /> : null}

      {!monthlyCost.isLoading && (notFound || !monthlyCost.data) ? (
        <Alert type="info" showIcon message={MESSAGES.monthlyCost.noDataYet} />
      ) : null}
      {monthlyCost.data ? <MonthlyCostSummaryPanel summary={monthlyCost.data} /> : null}

      {importOpen && (
        <MonthlyCostImportModal
          open
          periodMonth={periodMonth}
          onClose={() => setImportOpen(false)}
        />
      )}
    </PageCard>
  );
}
