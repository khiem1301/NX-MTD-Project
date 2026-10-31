import { Alert, Button, Card, Descriptions, Popconfirm, Space, Tag } from 'antd';
import { DownloadOutlined } from '@ant-design/icons';
import { MESSAGES } from '@/constants/messages';
import { formatDateTime, formatMonth } from '@/utils/format';
import { PAYROLL_STATUS_LABEL, PAYROLL_TEXT } from '../constants';
import { useDownloadImportedFile, useSubmitPayroll } from '../hooks/usePayroll';
import type { PayrollSummary } from '../types';
import { formatCurrency } from '../utils';

type Props = { periodMonth: string; summary: PayrollSummary };

/** UC31 — tình trạng bảng lương tháng hiện tại của Kế toán + nút gửi duyệt (Nháp → Chờ duyệt). */
export function PayrollSummaryPanel({ periodMonth, summary }: Props) {
  const submit = useSubmitPayroll(periodMonth);
  const downloadFile = useDownloadImportedFile('accountant');
  const canSubmit = summary.status === 'Draft' && summary.payslipCount > 0;

  return (
    <Card
      size="small"
      title={`${PAYROLL_TEXT.summary.periodMonth} ${formatMonth(summary.periodMonth)}`}
    >
      <Descriptions
        column={{ xs: 1, sm: 2, md: 3 }}
        bordered
        size="small"
        style={{ marginBottom: 16 }}
      >
        <Descriptions.Item label={PAYROLL_TEXT.summary.status}>
          <Tag color={PAYROLL_STATUS_LABEL[summary.status].color}>
            {PAYROLL_STATUS_LABEL[summary.status].text}
          </Tag>
        </Descriptions.Item>
        <Descriptions.Item label={PAYROLL_TEXT.summary.totalAmount}>
          {formatCurrency(summary.totalAmount)}
        </Descriptions.Item>
        <Descriptions.Item label={PAYROLL_TEXT.summary.payslipCount}>
          {summary.payslipCount}
        </Descriptions.Item>
        {summary.submittedAt ? (
          <Descriptions.Item label={PAYROLL_TEXT.summary.submittedAt}>
            {formatDateTime(summary.submittedAt)}
          </Descriptions.Item>
        ) : null}
        {summary.approvedAt ? (
          <Descriptions.Item label={PAYROLL_TEXT.summary.approvedAt}>
            {formatDateTime(summary.approvedAt)}
          </Descriptions.Item>
        ) : null}
      </Descriptions>

      {summary.rejectReason ? (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 16 }}
          message={`${PAYROLL_TEXT.summary.rejectReason}: ${summary.rejectReason}`}
        />
      ) : null}
      {summary.reopenReason ? (
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 16 }}
          message={`${PAYROLL_TEXT.summary.reopenReason}: ${summary.reopenReason}`}
        />
      ) : null}

      <Space>
        <Popconfirm
          title={MESSAGES.payroll.confirmSubmit}
          disabled={!canSubmit}
          onConfirm={() => submit.mutate(summary.payrollId)}
        >
          <Button type="primary" disabled={!canSubmit} loading={submit.isPending}>
            {PAYROLL_TEXT.submit}
          </Button>
        </Popconfirm>
        {summary.hasImportedFile ? (
          <Button
            icon={<DownloadOutlined />}
            loading={downloadFile.isPending}
            onClick={() =>
              downloadFile.mutate({
                payrollId: summary.payrollId,
                periodMonth: summary.periodMonth,
              })
            }
          >
            {PAYROLL_TEXT.downloadImportedFile}
          </Button>
        ) : null}
      </Space>
    </Card>
  );
}
