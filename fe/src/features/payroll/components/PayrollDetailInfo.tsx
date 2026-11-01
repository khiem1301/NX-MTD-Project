import { Descriptions, Tag } from 'antd';
import { formatCurrency, formatDateTime } from '@/utils/format';
import { PAYROLL_STATUS_LABEL, PAYROLL_TEXT } from '../constants';
import type { PayrollDetail } from '../types';

/** Thông tin chung của một bảng lương: trạng thái, tổng tiền, số phiếu, mốc gửi duyệt / duyệt. */
export function PayrollDetailInfo({ detail: d }: { detail: PayrollDetail }) {
  return (
    <Descriptions
      column={{ xs: 1, sm: 2, md: 3 }}
      bordered
      size="small"
      style={{ marginBottom: 16 }}
    >
      <Descriptions.Item label={PAYROLL_TEXT.summary.status}>
        <Tag color={PAYROLL_STATUS_LABEL[d.status].color}>
          {PAYROLL_STATUS_LABEL[d.status].text}
        </Tag>
      </Descriptions.Item>
      <Descriptions.Item label={PAYROLL_TEXT.summary.totalAmount}>
        {formatCurrency(d.totalAmount)}
      </Descriptions.Item>
      <Descriptions.Item label={PAYROLL_TEXT.summary.payslipCount}>
        {d.payslips.length}
      </Descriptions.Item>
      {d.submittedAt ? (
        <Descriptions.Item label={PAYROLL_TEXT.summary.submittedAt}>
          {formatDateTime(d.submittedAt)}
        </Descriptions.Item>
      ) : null}
      {d.approvedAt ? (
        <Descriptions.Item label={PAYROLL_TEXT.summary.approvedAt}>
          {formatDateTime(d.approvedAt)}
        </Descriptions.Item>
      ) : null}
    </Descriptions>
  );
}
