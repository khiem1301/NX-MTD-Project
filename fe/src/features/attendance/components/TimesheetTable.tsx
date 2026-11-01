import { Alert, Empty, Space, Tag, Tooltip, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { Paginated } from '@/types/api';
import { ATTENDANCE_TEXT, DEFAULT_PAGE_SIZE } from '../constants';
import type { DailyTimesheet } from '../types';
import { formatDate, formatHours, formatNumber, formatTime, workdayTone } from '../utils';
import { DataTable } from '@/components/common/DataTable';
import { StatusTags } from './StatusTags';

const WORKDAY_COLOR = {
  full: '#52c41a',
  partial: '#fa8c16',
  none: '#bfbfbf',
} as const;

type Props = {
  data: Paginated<DailyTimesheet> | undefined;
  isLoading: boolean;
  isFetching: boolean;
  error: { message: string } | null;
  onPageChange: (page: number, pageSize: number) => void;
  /** Màn "của tôi" đã biết người là ai nên ẩn cột nhân viên và tổ. */
  showEmployee?: boolean;
};

export function TimesheetTable({
  data,
  isLoading,
  isFetching,
  error,
  onPageChange,
  showEmployee = true,
}: Props) {
  if (error) {
    return <Alert type="error" showIcon message={error.message} />;
  }

  const columns: ColumnsType<DailyTimesheet> = [
    {
      title: ATTENDANCE_TEXT.table.workDate,
      dataIndex: 'workDate',
      width: 130,
      fixed: 'left',
      render: (value: string, row) => (
        <Space size={6}>
          <span style={{ fontWeight: row.isSunday ? 600 : 400 }}>{formatDate(value)}</span>
          {row.isSunday ? <Tag color="red">{ATTENDANCE_TEXT.tag.sunday}</Tag> : null}
        </Space>
      ),
    },
    ...(showEmployee
      ? ([
          {
            title: ATTENDANCE_TEXT.table.employee,
            dataIndex: 'fullName',
            render: (value: string, row) => (
              <div>
                <div style={{ fontWeight: 500 }}>{value}</div>
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  {row.employeeCode}
                </Typography.Text>
              </div>
            ),
          },
          {
            title: ATTENDANCE_TEXT.table.team,
            dataIndex: 'teamName',
            width: 140,
            responsive: ['lg'],
            render: (value: string | null) => value ?? '—',
          },
        ] satisfies ColumnsType<DailyTimesheet>)
      : []),
    {
      title: ATTENDANCE_TEXT.table.checkIn,
      dataIndex: 'checkIn',
      width: 92,
      align: 'center',
      render: (value: string | null) => formatTime(value),
    },
    {
      title: ATTENDANCE_TEXT.table.checkOut,
      dataIndex: 'checkOut',
      width: 92,
      align: 'center',
      render: (value: string | null, row) =>
        row.isMissingPunch ? (
          <Tooltip title={ATTENDANCE_TEXT.tag.missingPunch}>
            <Typography.Text type="warning">—</Typography.Text>
          </Tooltip>
        ) : (
          formatTime(value)
        ),
    },
    {
      title: ATTENDANCE_TEXT.table.workedHours,
      dataIndex: 'workedHours',
      width: 96,
      align: 'right',
      responsive: ['md'],
      render: (value: number) => formatHours(value),
    },
    {
      title: ATTENDANCE_TEXT.table.workdayValue,
      dataIndex: 'workdayValue',
      width: 86,
      align: 'right',
      render: (value: number) => (
        <span style={{ color: WORKDAY_COLOR[workdayTone(value)], fontWeight: 600 }}>
          {formatNumber(value)}
        </span>
      ),
    },
    {
      title: ATTENDANCE_TEXT.table.overtime,
      dataIndex: 'overtimeHours',
      width: 96,
      align: 'right',
      responsive: ['md'],
      render: (value: number) =>
        value > 0 ? <Tag color="blue">{formatHours(value)}</Tag> : <span>—</span>,
    },
    {
      title: ATTENDANCE_TEXT.table.status,
      key: 'status',
      width: 170,
      render: (_, row) => <StatusTags row={row} />,
    },
    {
      title: ATTENDANCE_TEXT.table.note,
      dataIndex: 'note',
      responsive: ['xl'],
      render: (value: string | null) => value ?? '—',
    },
  ];

  return (
    <DataTable<DailyTimesheet>
      rowKey="dailyTimesheetId"
      size="small"
      columns={columns}
      dataSource={data?.items ?? []}
      loading={isLoading || isFetching}
      scroll={{ x: 'max-content' }}
      locale={{ emptyText: <Empty description={ATTENDANCE_TEXT.tabs.timesheet} /> }}
      rowClassName={(row) => (row.isMissingPunch ? 'nx-row-warning' : '')}
      pagination={{
        current: data?.page ?? 1,
        pageSize: data?.pageSize ?? DEFAULT_PAGE_SIZE,
        total: data?.totalItems ?? 0,
        showSizeChanger: true,
        pageSizeOptions: [20, 50, 100],
        showTotal: (total, [from, to]) => `${from}–${to} / ${total} dòng`,
        onChange: onPageChange,
      }}
    />
  );
}
