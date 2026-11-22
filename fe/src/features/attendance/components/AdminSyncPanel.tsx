import { Space, Typography } from 'antd';
import { ATTENDANCE_TEXT } from '../constants';
import { ExcelImportPanel } from './ExcelImportPanel';
import { SyncSessionTable } from './SyncSessionTable';

/** Tab vận hành chỉ Admin: lịch sử phiên đồng bộ + nhập Excel dự phòng. */
export function AdminSyncPanel() {
  return (
    <Space direction="vertical" size={24} style={{ width: '100%' }}>
      <div>
        <Typography.Title level={5} style={{ marginTop: 0 }}>
          {ATTENDANCE_TEXT.tabs.sessions}
        </Typography.Title>
        <SyncSessionTable />
      </div>
      <ExcelImportPanel />
    </Space>
  );
}
