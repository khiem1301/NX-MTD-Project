import { useState } from 'react';
import { Button, Card, Select, Table, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { formatDateTime } from '@/utils/format';
import { PAYROLL_STATUS_FILTER_OPTIONS, PAYROLL_STATUS_LABEL, PAYROLL_TEXT } from '../constants';
import { useAdminPayrolls } from '../hooks/usePayroll';
import type { PayrollStatus, PayrollSummary } from '../types';
import { formatCurrency } from '../utils';

type Props = { onView: (payrollId: number) => void };

const DEFAULT_STATUS: PayrollStatus = 'PendingApproval';

/** UC32 — Admin: danh sách bảng lương theo trạng thái; mặc định xem các bảng Chờ duyệt. */
export function PendingPayrollTable({ onView }: Props) {
  const [status, setStatus] = useState<PayrollStatus | undefined>(DEFAULT_STATUS);
  const payrolls = useAdminPayrolls(status);

  const columns: ColumnsType<PayrollSummary> = [
    { title: PAYROLL_TEXT.periodMonth, dataIndex: 'periodMonth' },
    {
      title: PAYROLL_TEXT.summary.status,
      dataIndex: 'status',
      render: (v: PayrollStatus) => (
        <Tag color={PAYROLL_STATUS_LABEL[v].color}>{PAYROLL_STATUS_LABEL[v].text}</Tag>
      ),
    },
    {
      title: PAYROLL_TEXT.summary.totalAmount,
      dataIndex: 'totalAmount',
      align: 'right',
      render: (v: number) => formatCurrency(v),
    },
    { title: PAYROLL_TEXT.summary.payslipCount, dataIndex: 'payslipCount', align: 'right' },
    {
      title: PAYROLL_TEXT.summary.submittedAt,
      dataIndex: 'submittedAt',
      render: (v: string | null) => (v ? formatDateTime(v) : '—'),
    },
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, row) => (
        <Button type="link" onClick={() => onView(row.payrollId)}>
          {PAYROLL_TEXT.table.viewDetail}
        </Button>
      ),
    },
  ];

  return (
    <Card
      size="small"
      extra={
        <Select<PayrollStatus | undefined>
          allowClear
          placeholder={PAYROLL_TEXT.filter.statusPlaceholder}
          value={status}
          options={PAYROLL_STATUS_FILTER_OPTIONS}
          onChange={setStatus}
          style={{ width: 180 }}
        />
      }
    >
      <QueryError error={payrolls.error} onRetry={() => void payrolls.refetch()} />
      <Table<PayrollSummary>
        rowKey="payrollId"
        size="middle"
        columns={columns}
        dataSource={payrolls.data?.items}
        loading={payrolls.isFetching}
        pagination={false}
        locale={{ emptyText: MESSAGES.common.empty }}
        scroll={{ x: 'max-content' }}
      />
    </Card>
  );
}
