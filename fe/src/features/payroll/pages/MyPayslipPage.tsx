import { useState } from 'react';
import { Alert, Card, Empty, List, Spin, Typography } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { formatDateTime, formatMonth } from '@/utils/format';
import { PayslipItemsView } from '../components/PayslipItemsView';
import { PAYROLL_TEXT } from '../constants';
import { useMyPayslip, useMyPayslips } from '../hooks/usePayroll';
import { formatCurrency } from '../utils';
import { PageCard } from '@/components/common/PageCard';

/** UC06 — mọi vai trò xem phiếu lương của chính mình, chỉ xem được các tháng Admin đã duyệt. */
export function MyPayslipPage() {
  const summaries = useMyPayslips();
  const [selectedMonth, setSelectedMonth] = useState<string | null>(null);
  const detail = useMyPayslip(selectedMonth);

  const rows = summaries.data ?? [];

  return (
    <PageCard
      title={PAYROLL_TEXT.myPayslipPageTitle}
      description={PAYROLL_TEXT.myPayslipPageSubtitle}
    >
      {summaries.error ? <Alert type="error" showIcon message={summaries.error.message} /> : null}
      {summaries.isLoading ? <Spin /> : null}

      {!summaries.isLoading && rows.length === 0 ? (
        <Empty description={MESSAGES.payroll.noPayslip} />
      ) : null}

      {rows.length > 0 ? (
        <Card size="small">
          <List
            dataSource={rows}
            rowKey="periodMonth"
            renderItem={(item) => (
              <List.Item
                onClick={() => setSelectedMonth(item.periodMonth)}
                style={{
                  cursor: 'pointer',
                  background: selectedMonth === item.periodMonth ? '#f5f5f5' : undefined,
                }}
              >
                <List.Item.Meta
                  title={`${PAYROLL_TEXT.myPayslip.periodMonth} ${formatMonth(item.periodMonth)}`}
                  description={`${PAYROLL_TEXT.myPayslip.approvedAt}: ${formatDateTime(item.approvedAt)}`}
                />
                <Typography.Text strong>{formatCurrency(item.netPay)}</Typography.Text>
              </List.Item>
            )}
          />
        </Card>
      ) : null}

      {selectedMonth ? (
        <Card
          size="small"
          title={`${PAYROLL_TEXT.myPayslip.periodMonth} ${formatMonth(selectedMonth)}`}
        >
          {detail.isLoading ? <Spin /> : null}
          {detail.error ? <Alert type="error" showIcon message={detail.error.message} /> : null}
          {detail.data ? (
            <PayslipItemsView
              workDays={detail.data.workDays}
              totalAdditions={detail.data.totalAdditions}
              totalDeductions={detail.data.totalDeductions}
              insurance={detail.data.insurance}
              netPay={detail.data.netPay}
              items={detail.data.items}
            />
          ) : null}
        </Card>
      ) : null}
    </PageCard>
  );
}
