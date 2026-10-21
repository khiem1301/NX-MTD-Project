import { Alert, Empty, Table, Tag, Tooltip, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { ATTENDANCE_TEXT } from '../constants';
import { useSyncSessions } from '../hooks/useAttendance';
import type { SyncSession, SyncStatus } from '../types';
import { formatDateTime } from '../utils';

const STATUS_TAG: Record<SyncStatus, { color: string; text: string }> = {
  Running: { color: 'processing', text: ATTENDANCE_TEXT.sync.statusRunning },
  Succeeded: { color: 'success', text: ATTENDANCE_TEXT.sync.statusSucceeded },
  Failed: { color: 'error', text: ATTENDANCE_TEXT.sync.statusFailed },
};

/** Lịch sử đồng bộ JOB-01 và các lần nhập file Excel dự phòng. */
export function SyncSessionTable() {
  const { data, isLoading, error } = useSyncSessions();

  if (error) {
    return <Alert type="error" showIcon message={(error as { message: string }).message} />;
  }

  const columns: ColumnsType<SyncSession> = [
    {
      title: ATTENDANCE_TEXT.table.startedAt,
      dataIndex: 'startedAt',
      width: 160,
      render: (value: string) => formatDateTime(value),
    },
    {
      title: ATTENDANCE_TEXT.table.source,
      dataIndex: 'source',
      width: 140,
      render: (value: SyncSession['source'], row) =>
        value === 'ExcelImport' ? (
          <Tooltip title={row.fileName ?? undefined}>
            <Tag color="purple">{ATTENDANCE_TEXT.sync.sourceExcel}</Tag>
          </Tooltip>
        ) : (
          <Tag color="blue">{ATTENDANCE_TEXT.sync.sourceRjSync}</Tag>
        ),
    },
    {
      title: ATTENDANCE_TEXT.table.syncStatus,
      dataIndex: 'status',
      width: 130,
      render: (value: SyncStatus, row) => {
        const tag = STATUS_TAG[value];
        return row.errorMessage ? (
          <Tooltip title={row.errorMessage}>
            <Tag color={tag.color}>{tag.text}</Tag>
          </Tooltip>
        ) : (
          <Tag color={tag.color}>{tag.text}</Tag>
        );
      },
    },
    {
      title: ATTENDANCE_TEXT.table.received,
      dataIndex: 'recordsReceived',
      width: 100,
      align: 'right',
    },
    {
      title: ATTENDANCE_TEXT.table.inserted,
      dataIndex: 'recordsInserted',
      width: 110,
      align: 'right',
      render: (value: number) =>
        value > 0 ? <Typography.Text strong>{value}</Typography.Text> : value,
    },
    {
      title: ATTENDANCE_TEXT.table.finishedAt,
      dataIndex: 'finishedAt',
      width: 160,
      responsive: ['lg'],
      render: (value: string | null) => formatDateTime(value),
    },
    {
      title: ATTENDANCE_TEXT.table.lastRecordId,
      dataIndex: 'lastSourceRecordId',
      width: 110,
      align: 'right',
      responsive: ['xl'],
      render: (value: number | null) => value ?? '—',
    },
  ];

  return (
    <Table<SyncSession>
      rowKey="syncSessionId"
      size="middle"
      columns={columns}
      dataSource={data ?? []}
      loading={isLoading}
      pagination={false}
      scroll={{ x: 'max-content' }}
      locale={{
        emptyText: (
          <Empty
            image={Empty.PRESENTED_IMAGE_SIMPLE}
            description={ATTENDANCE_TEXT.sync.never}
          />
        ),
      }}
    />
  );
}
