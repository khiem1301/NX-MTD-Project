import { useState } from 'react';
import { Alert, Button, Space, Spin, Tabs, Typography } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { DownloadOutlined } from '@ant-design/icons';
import type { ApiError } from '@/types/api';
import { MealReportPanel } from '../components/MealReportPanel';
import { VehicleReportTable } from '../components/VehicleReportTable';
import { WorkdayOvertimeTable } from '../components/WorkdayOvertimeTable';
import { ACCOUNTING_REPORT_TEXT } from '../constants';
import {
  useExportAccountingReports,
  useMealReport,
  useVehicleReport,
  useWorkdayOvertimeReport,
} from '../hooks/useAccountingReports';
import type { AccountingReportFilter } from '../types';
import { PERIOD_MONTH_FORMAT, currentPeriodMonth, toPeriodMonth } from '../utils';
import { PageCard } from '@/components/common/PageCard';
import { AppMonthPicker } from '@/components/common/DateInputs';

/** UC36 — Kế toán: chọn tháng, xem 3 báo cáo vận hành (công/tăng ca, suất ăn, sổ xe), xuất Excel. */
export function AccountingReportsPage() {
  const [filter, setFilter] = useState<AccountingReportFilter>(() => ({
    periodMonth: toPeriodMonth(currentPeriodMonth()),
  }));

  const workdays = useWorkdayOvertimeReport(filter);
  const meals = useMealReport(filter);
  const vehicles = useVehicleReport(filter);
  const exportReports = useExportAccountingReports();
  const workdaysError = workdays.error as ApiError | null;

  const items = [
    {
      key: 'workdays',
      label: ACCOUNTING_REPORT_TEXT.tabs.workdays,
      children: workdays.isLoading ? (
        <Spin />
      ) : workdays.data ? (
        <WorkdayOvertimeTable report={workdays.data} />
      ) : (
        <Alert type="info" showIcon message={ACCOUNTING_REPORT_TEXT.noDataYet} />
      ),
    },
    {
      key: 'meals',
      label: ACCOUNTING_REPORT_TEXT.tabs.meals,
      children: meals.isLoading ? (
        <Spin />
      ) : meals.data ? (
        <MealReportPanel report={meals.data} />
      ) : (
        <Alert type="info" showIcon message={ACCOUNTING_REPORT_TEXT.noDataYet} />
      ),
    },
    {
      key: 'vehicles',
      label: ACCOUNTING_REPORT_TEXT.tabs.vehicles,
      children: vehicles.isLoading ? (
        <Spin />
      ) : vehicles.data ? (
        <VehicleReportTable report={vehicles.data} />
      ) : (
        <Alert type="info" showIcon message={ACCOUNTING_REPORT_TEXT.noDataYet} />
      ),
    },
  ];

  return (
    <PageCard
      title={ACCOUNTING_REPORT_TEXT.pageTitle}
      description={ACCOUNTING_REPORT_TEXT.pageSubtitle}
    >
      <Space wrap style={{ justifyContent: 'space-between', width: '100%' }}>
        <Space>
          <Typography.Text>{ACCOUNTING_REPORT_TEXT.periodMonth}</Typography.Text>
          <AppMonthPicker
            allowClear={false}
            value={dayjs(filter.periodMonth, PERIOD_MONTH_FORMAT)}
            onChange={(value: Dayjs | null) => {
              if (value) setFilter({ periodMonth: toPeriodMonth(value) });
            }}
          />
        </Space>
        <Button
          icon={<DownloadOutlined />}
          loading={exportReports.isPending}
          onClick={() => exportReports.mutate(filter)}
        >
          {ACCOUNTING_REPORT_TEXT.export}
        </Button>
      </Space>

      {workdaysError ? <Alert type="error" showIcon message={workdaysError.message} /> : null}

      <Tabs items={items} />
    </PageCard>
  );
}
