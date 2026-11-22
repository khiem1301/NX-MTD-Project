import { useMemo } from 'react';
import { Badge, Tabs } from 'antd';
import {
  CloudSyncOutlined,
  EditOutlined,
  TeamOutlined,
  TableOutlined,
} from '@ant-design/icons';
import { ROLES } from '@/constants/roles';
import { useAuth } from '@/hooks/useAuth';
import { useFromRequest } from '@/hooks/useFromRequest';
import { PageCard } from '@/components/common/PageCard';
import { ATTENDANCE_TEXT } from '../constants';
import { useSyncHealth } from '../hooks/useAttendance';
import { AdjustmentPanel } from '../components/AdjustmentPanel';
import { AdminSyncPanel } from '../components/AdminSyncPanel';
import { RjEmployeePanel } from '../components/RjEmployeePanel';
import { SyncHealthBanner } from '../components/SyncHealthBanner';
import { TimesheetView } from '../components/TimesheetView';

/** UC28 — bảng công, nhân viên RJ, chấm công hộ; Admin thêm tab Đồng bộ (lịch sử + Excel). */
export function AttendancePage() {
  const { user } = useAuth();
  const isAdmin = user?.role === ROLES.Admin;
  const { data: health } = useSyncHealth();
  const { requestId: presetRequestId } = useFromRequest();

  const items = useMemo(() => {
    const shared = [
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
        key: 'adjustments',
        label: (
          <span>
            <EditOutlined /> {ATTENDANCE_TEXT.tabs.adjustments}
          </span>
        ),
        children: <AdjustmentPanel presetRequestId={presetRequestId} />,
      },
    ];

    if (!isAdmin) return shared;

    return [
      ...shared,
      {
        key: 'sync',
        label: (
          <span>
            <CloudSyncOutlined /> {ATTENDANCE_TEXT.tabs.sync}
          </span>
        ),
        children: <AdminSyncPanel />,
      },
    ];
  }, [health?.unmappedCodeCount, isAdmin, presetRequestId]);

  return (
    <PageCard title={ATTENDANCE_TEXT.pageTitle} description={ATTENDANCE_TEXT.pageSubtitle}>
      <SyncHealthBanner health={health} />
      <Tabs
        defaultActiveKey={presetRequestId === null ? 'timesheet' : 'adjustments'}
        items={items}
      />
    </PageCard>
  );
}
