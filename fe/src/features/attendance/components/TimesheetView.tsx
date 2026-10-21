import { Space } from 'antd';
import {
  ClockCircleOutlined,
  FieldTimeOutlined,
  TeamOutlined,
  WarningOutlined,
} from '@ant-design/icons';
import type { ApiError } from '@/types/api';
import type { TimesheetAudience } from '../api/attendanceApi';
import { ATTENDANCE_TEXT } from '../constants';
import { useTimesheets, useTimesheetSummary } from '../hooks/useAttendance';
import { useTimesheetFilter } from '../hooks/useTimesheetFilter';
import { formatNumber } from '../utils';
import { StatTiles, type StatTile } from './StatTiles';
import { TimesheetFilterBar } from './TimesheetFilterBar';
import { TimesheetTable } from './TimesheetTable';

type Props = {
  audience: TimesheetAudience;
  /** Màn "của tôi" chỉ có một người nên bỏ ô tìm kiếm, cột nhân viên và thẻ đếm người. */
  personal?: boolean;
};

/** Bảng công dùng chung cho Kế toán, tổ trưởng và cá nhân; khác nhau ở endpoint và phạm vi. */
export function TimesheetView({ audience, personal = false }: Props) {
  const filterState = useTimesheetFilter();
  const timesheets = useTimesheets(audience, filterState.filter);
  const summary = useTimesheetSummary(audience, filterState.filter);

  const data = summary.data;
  const tiles: StatTile[] = [
    ...(personal
      ? []
      : [
          {
            key: 'employees',
            label: ATTENDANCE_TEXT.stats.employees,
            value: data ? formatNumber(data.employeeCount, 0) : '—',
            icon: <TeamOutlined />,
            tone: 'primary' as const,
          },
        ]),
    {
      key: 'workdays',
      label: ATTENDANCE_TEXT.stats.workdays,
      value: data ? formatNumber(data.totalWorkdays) : '—',
      suffix: 'công',
      icon: <ClockCircleOutlined />,
      tone: 'success',
    },
    {
      key: 'overtime',
      label: ATTENDANCE_TEXT.stats.overtime,
      value: data ? formatNumber(data.totalOvertimeHours) : '—',
      suffix: 'giờ',
      icon: <FieldTimeOutlined />,
      tone: 'neutral',
    },
    {
      key: 'missing',
      label: ATTENDANCE_TEXT.stats.missingPunch,
      value: data ? formatNumber(data.missingPunchCount, 0) : '—',
      suffix: 'dòng',
      icon: <WarningOutlined />,
      tone: 'warning',
    },
  ];

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <StatTiles tiles={tiles} loading={summary.isLoading} />

      <TimesheetFilterBar
        range={filterState.range}
        keyword={filterState.keyword}
        onlyMissingPunch={filterState.onlyMissingPunch}
        onRangeChange={filterState.setRange}
        onKeywordChange={filterState.setKeyword}
        onOnlyMissingChange={filterState.setOnlyMissingPunch}
        onReset={filterState.reset}
        showKeyword={!personal}
      />

      <TimesheetTable
        data={timesheets.data}
        isLoading={timesheets.isLoading}
        isFetching={timesheets.isFetching}
        error={(timesheets.error as ApiError | null) ?? null}
        onPageChange={filterState.changePage}
        showEmployee={!personal}
      />
    </Space>
  );
}
