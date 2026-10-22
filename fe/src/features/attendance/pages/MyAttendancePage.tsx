import { Space, Typography } from 'antd';
import { ATTENDANCE_TEXT } from '../constants';
import { TimesheetView } from '../components/TimesheetView';

/** UC58 — ngày công của tôi; mọi vai trò xem được nhưng chỉ dữ liệu của chính mình. */
export function MyAttendancePage() {
  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div>
        <Typography.Title level={3} style={{ marginBottom: 4 }}>
          {ATTENDANCE_TEXT.myPageTitle}
        </Typography.Title>
        <Typography.Text type="secondary">{ATTENDANCE_TEXT.pageSubtitle}</Typography.Text>
      </div>
      <TimesheetView audience="me" personal />
    </Space>
  );
}
