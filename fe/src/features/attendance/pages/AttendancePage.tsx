import { Badge, Tabs } from 'antd';
import {
  EditOutlined,
  FileExcelOutlined,
  HistoryOutlined,
  TeamOutlined,
  TableOutlined,
} from '@ant-design/icons';
import { ATTENDANCE_TEXT } from '../constants';
import { useSyncHealth } from '../hooks/useAttendance';
import { AdjustmentPanel } from '../components/AdjustmentPanel';
import { ExcelImportPanel } from '../components/ExcelImportPanel';
import { RjEmployeePanel } from '../components/RjEmployeePanel';
import { SyncHealthBanner } from '../components/SyncHealthBanner';
import { SyncSessionTable } from '../components/SyncSessionTable';
import { TimesheetView } from '../components/TimesheetView';
import { PageCard } from '@/components/common/PageCard';

/** UC28 — bảng công, tạo công nhân từ RJ, lịch sử đồng bộ (không còn gán mã tay). */
export function AttendancePage() {
  const { data: health } = useSyncHealth();

  return (
    <PageCard title={ATTENDANCE_TEXT.pageTitle} description={ATTENDANCE_TEXT.pageSubtitle}>
      <SyncHealthBanner health={health} />

      <Tabs
        defaultActiveKey="rj-employees"
        items={[
          {
            key: 'rj-employees',
            label: (
              <Badge count={health?.unmappedCodeCount ?? 0} offset={[10, -2]} size="small">
                <span>
                  <TeamOutlined /> {ATTENDANCE_TEXT.tabs.rjEmployees}
                </span>
              </Badge>
            ),
            children: <RjEmployeePanel />,
          },
          {
            key: 'timesheet',
            label: (
              <span>
                <TableOutlined /> {ATTENDANCE_TEXT.tabs.timesheet}
              </span>
            ),
            children: <TimesheetView audience="accountant" />,
          },
          {
            key: 'sessions',
            label: (
              <span>
                <HistoryOutlined /> {ATTENDANCE_TEXT.tabs.sessions}
              </span>
            ),
            children: <SyncSessionTable />,
          },
          {
            key: 'adjustments',
            label: (
              <span>
                <EditOutlined /> {ATTENDANCE_TEXT.tabs.adjustments}
              </span>
            ),
            children: <AdjustmentPanel />,
          },
          {
            key: 'import',
            label: (
              <span>
                <FileExcelOutlined /> {ATTENDANCE_TEXT.tabs.import}
              </span>
            ),
            children: <ExcelImportPanel />,
          },
        ]}
      />
    </PageCard>
  );
}
