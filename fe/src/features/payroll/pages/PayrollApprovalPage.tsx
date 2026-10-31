import { useState } from 'react';
import { PayrollDetailModal } from '../components/PayrollDetailModal';
import { PendingPayrollTable } from '../components/PendingPayrollTable';
import { PAYROLL_TEXT } from '../constants';
import { PageCard } from '@/components/common/PageCard';

/** UC32 — Admin: xem bảng lương theo trạng thái, duyệt (khóa tháng), từ chối hoặc mở lại khi sai. */
export function PayrollApprovalPage() {
  const [viewingId, setViewingId] = useState<number | null>(null);

  return (
    <PageCard title={PAYROLL_TEXT.adminPageTitle} description={PAYROLL_TEXT.adminPageSubtitle}>
      <PendingPayrollTable onView={setViewingId} />
      <PayrollDetailModal payrollId={viewingId} onClose={() => setViewingId(null)} />
    </PageCard>
  );
}
