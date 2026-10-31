import { ATTENDANCE_TEXT } from '../constants';
import { TimesheetView } from '../components/TimesheetView';
import { PageCard } from '@/components/common/PageCard';

/** UC28 — tổ trưởng / tổ phó xem bảng công tổ mình (GBR-01, phạm vi do BE ép). */
export function TeamAttendancePage() {
  return (
    <PageCard title={ATTENDANCE_TEXT.teamPageTitle} description={ATTENDANCE_TEXT.pageSubtitle}>
      <TimesheetView audience="teamLeader" />
    </PageCard>
  );
}
