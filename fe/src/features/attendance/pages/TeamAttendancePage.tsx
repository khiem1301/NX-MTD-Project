import { Space, Typography } from 'antd';
import { ATTENDANCE_TEXT } from '../constants';
import { TimesheetView } from '../components/TimesheetView';

/** UC28 — tổ trưởng / tổ phó xem bảng công tổ mình (GBR-01, phạm vi do BE ép). */
export function TeamAttendancePage() {
  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div>
        <Typography.Title level={3} style={{ marginBottom: 4 }}>
          {ATTENDANCE_TEXT.teamPageTitle}
        </Typography.Title>
        <Typography.Text type="secondary">{ATTENDANCE_TEXT.pageSubtitle}</Typography.Text>
      </div>
      <TimesheetView audience="teamLeader" />
    </Space>
  );
}
