import { Badge, Tabs } from 'antd';
import {
  EditOutlined,
  FileExcelOutlined,
  HistoryOutlined,
  LinkOutlined,
  TableOutlined,
} from '@ant-design/icons';
import { ATTENDANCE_TEXT } from '../constants';
import { useSyncHealth } from '../hooks/useAttendance';
import { AdjustmentPanel } from '../components/AdjustmentPanel';
import { ExcelImportPanel } from '../components/ExcelImportPanel';
import { SyncHealthBanner } from '../components/SyncHealthBanner';
import { SyncSessionTable } from '../components/SyncSessionTable';
import { TimesheetView } from '../components/TimesheetView';
import { UnmappedCodePanel } from '../components/UnmappedCodePanel';
import { PageCard } from '@/components/common/PageCard';

/** UC28 — màn chấm công của Kế toán: bảng công toàn xưởng, gán mã, lịch sử đồng bộ. */
export function AttendancePage() {
  const { data: health } = useSyncHealth();

  return (
    <PageCard title={ATTENDANCE_TEXT.pageTitle} description={ATTENDANCE_TEXT.pageSubtitle}>
      <SyncHealthBanner health={health} />

      <Tabs
        defaultActiveKey="timesheet"
        items={[
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
            key: 'unmapped',
            label: (
              <Badge count={health?.unmappedCodeCount ?? 0} offset={[10, -2]} size="small">
                <span>
                  <LinkOutlined /> {ATTENDANCE_TEXT.tabs.unmapped}
                </span>
              </Badge>
            ),
            children: <UnmappedCodePanel />,
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
