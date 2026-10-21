import { useState } from 'react';
import { Space, Typography } from 'antd';
import { PayrollDetailDrawer } from '../components/PayrollDetailDrawer';
import { PendingPayrollTable } from '../components/PendingPayrollTable';
import { PAYROLL_TEXT } from '../constants';

/** UC32 — Admin: xem bảng lương theo trạng thái, duyệt (khóa tháng), từ chối hoặc mở lại khi sai. */
export function PayrollApprovalPage() {
  const [viewingId, setViewingId] = useState<number | null>(null);

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div>
        <Typography.Title level={3} style={{ marginBottom: 4 }}>
          {PAYROLL_TEXT.adminPageTitle}
        </Typography.Title>
        <Typography.Text type="secondary">{PAYROLL_TEXT.adminPageSubtitle}</Typography.Text>
      </div>

      <PendingPayrollTable onView={setViewingId} />
      <PayrollDetailDrawer payrollId={viewingId} onClose={() => setViewingId(null)} />
    </Space>
  );
}
