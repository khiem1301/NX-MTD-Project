import { Alert, Empty, Space, Tag, Tooltip, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { Paginated } from '@/types/api';
import { ATTENDANCE_TEXT, DEFAULT_PAGE_SIZE } from '../constants';
import type { DailyTimesheet } from '../types';
import { formatDate, formatNumber, formatTime } from '@/utils/format';
import { formatHours, workdayTone } from '../utils/timesheet';
import { STAT_CARD_CLASS } from '@/components/common/cardTable';
import { DataTable } from '@/components/common/DataTable';
import { TABLE_SORT, type ServerSort, type SortablePagination } from '@/utils/tableSort';

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
  /** Cột đang sắp xếp ở máy chủ và hàm đổi cột sắp xếp. */
  sort: ServerSort;
  onSortChange: (next: ServerSort) => void;
  /** Màn "của tôi" đã biết người là ai nên ẩn cột nhân viên. */
  showEmployee?: boolean;
  /** Ẩn cột tổ khi màn chấm công tổ / đã lọc 1 tổ. */
  showTeam?: boolean;
};

export function TimesheetTable({
  data,
  isLoading,
  isFetching,
  error,
  onPageChange,
  sort,
  onSortChange,
  showEmployee = true,
  showTeam = true,
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
        ] satisfies ColumnsType<DailyTimesheet>)
      : []),
    ...(showTeam
      ? ([
          {
            title: ATTENDANCE_TEXT.table.team,
            dataIndex: 'teamCode',
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
      title: ATTENDANCE_TEXT.table.note,
      dataIndex: 'note',
      responsive: ['xl'],
      render: (value: string | null) => value ?? '—',
    },
  ];

  const pagination: SortablePagination = {
    current: data?.page ?? 1,
    pageSize: data?.pageSize ?? DEFAULT_PAGE_SIZE,
    total: data?.totalItems ?? 0,
    showSizeChanger: true,
    pageSizeOptions: [20, 50, 100],
    showTotal: (total, [from, to]) => `${from}–${to} / ${total} dòng`,
    onChange: onPageChange,
    [TABLE_SORT]: { sort, onChange: onSortChange },
  };

  return (
    <DataTable<DailyTimesheet>
      className={STAT_CARD_CLASS}
      rowKey="dailyTimesheetId"
      size="small"
      columns={columns}
      dataSource={data?.items ?? []}
      loading={isLoading || isFetching}
      scroll={{ x: 'max-content' }}
      locale={{ emptyText: <Empty description={ATTENDANCE_TEXT.tabs.timesheet} /> }}
      rowClassName={(row) => (row.isMissingPunch ? 'nx-row-warning' : '')}
      pagination={pagination}
    />
  );
}
