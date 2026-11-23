import type { ReactNode } from 'react';
import { Alert, List, Modal, Space, Spin, Typography, theme } from 'antd';
import { CheckCircleFilled, CloseCircleFilled, ExclamationCircleFilled } from '@ant-design/icons';
import { Link } from 'react-router-dom';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { COST_SUMMARY_ROUTES, PAYROLL_ROUTES } from '@/constants/routes';
import { fill, formatCurrency, formatMonth, formatQty } from '@/utils/format';
import { BREAK_EVEN_TEXT } from '../constants';
import { useBreakEvenInputs, useCalculateBreakEven } from '../hooks/useBreakEven';
import type { BreakEvenInputs } from '../types';

const T = BREAK_EVEN_TEXT.modal;

type CheckState = 'ok' | 'missing' | 'warn';
type Check = { label: string; state: CheckState; text: ReactNode };

/** Bốn mục đầu vào: ba mục đầu bắt buộc (thiếu là khóa nút Tính), giờ công thiếu chỉ cảnh báo. */
const checksOf = (d: BreakEvenInputs): Check[] => [
  {
    label: T.payroll,
    state: d.payrollApproved ? 'ok' : 'missing',
    text: d.payrollApproved ? (
      T.payrollOk
    ) : (
      <Link to={PAYROLL_ROUTES.approval}>
        {T.payrollMissing} · {T.goPayroll}
      </Link>
    ),
  },
  {
    label: T.cost,
    state: d.costEntered ? 'ok' : 'missing',
    text: d.costEntered ? (
      <Link to={COST_SUMMARY_ROUTES.admin}>
        {fill(
          T.costOk,
          formatCurrency(d.fixedTotal),
          formatCurrency(d.variableTotal),
          d.variableProductCount,
        )}
      </Link>
    ) : (
      T.costMissing
    ),
  },
  {
    label: T.output,
    state: d.productCount > 0 ? 'ok' : 'missing',
    text:
      d.productCount > 0
        ? fill(T.outputOk, d.productCount, formatQty(d.finishedQty), formatCurrency(d.revenue))
        : T.outputMissing,
  },
  {
    label: T.hours,
    state: d.labourHours > 0 ? 'ok' : 'warn',
    text: d.labourHours > 0 ? fill(T.hoursOk, formatQty(d.labourHours)) : T.hoursMissing,
  },
];

/** UC11 — popup "Tính hòa vốn": xem dữ liệu đầu vào của tháng rồi tính cả hai cách chia định phí (DEV-ALL-169). */
export function BreakEvenCalculateModal({
  periodMonth,
  open,
  onClose,
}: {
  periodMonth: string;
  open: boolean;
  onClose: () => void;
}) {
  const { token } = theme.useToken();
  const inputs = useBreakEvenInputs(periodMonth, open);
  const calculate = useCalculateBreakEven(periodMonth);
  const icon: Record<CheckState, ReactNode> = {
    ok: <CheckCircleFilled style={{ color: token.colorSuccess }} />,
    missing: <CloseCircleFilled style={{ color: token.colorError }} />,
    warn: <ExclamationCircleFilled style={{ color: token.colorWarning }} />,
  };
  return (
    <Modal
      open={open}
      title={fill(T.title, formatMonth(periodMonth))}
      okText={T.submit}
      cancelText={MESSAGES.common.cancel}
      okButtonProps={{ disabled: !inputs.data?.canCalculate }}
      confirmLoading={calculate.isPending}
      onOk={() => calculate.mutate(undefined, { onSuccess: onClose })}
      onCancel={onClose}
      destroyOnHidden
    >
      <Typography.Text strong>{T.inputs}</Typography.Text>
      {inputs.isPending && <Spin style={{ display: 'block', margin: 16 }} />}
      {inputs.isError && <QueryError error={inputs.error} onRetry={() => void inputs.refetch()} />}
      {inputs.data && !inputs.data.periodExists && (
        <Alert type="warning" showIcon message={T.noPeriod} />
      )}
      {inputs.data?.periodLocked && <Alert type="info" showIcon message={T.locked} />}
      {inputs.data && (
        <List
          size="small"
          dataSource={checksOf(inputs.data)}
          renderItem={(c) => (
            <List.Item>
              <Space align="start">
                {icon[c.state]}
                <Typography.Text>{c.label}</Typography.Text>
              </Space>
              <Typography.Text type={c.state === 'missing' ? 'danger' : 'secondary'}>
                {c.text}
              </Typography.Text>
            </List.Item>
          )}
        />
      )}
      <Typography.Text strong style={{ display: 'block', marginTop: 12 }}>
        {T.method}
      </Typography.Text>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
        {T.methodText}
      </Typography.Paragraph>
      <Alert type="info" showIcon message={T.history} />
    </Modal>
  );
}
