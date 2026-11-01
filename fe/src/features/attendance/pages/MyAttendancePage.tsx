import { ATTENDANCE_TEXT } from '../constants';
import { TimesheetView } from '../components/TimesheetView';
import { PageCard } from '@/components/common/PageCard';

/** UC58 — ngày công của tôi; mọi vai trò xem được nhưng chỉ dữ liệu của chính mình. */
export function MyAttendancePage() {
  return (
    <PageCard title={ATTENDANCE_TEXT.myPageTitle} description={ATTENDANCE_TEXT.pageSubtitle}>
      <TimesheetView audience="me" personal />
    </PageCard>
  );
}
