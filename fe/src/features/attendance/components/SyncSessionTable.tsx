import { useState } from 'react';
import { Alert, Empty, Tag, Tooltip, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { ATTENDANCE_TEXT } from '../constants';
import { useSyncSessions } from '../hooks/useAttendance';
import type { SyncSession, SyncStatus } from '../types';
import { formatDateTime } from '@/utils/format';
import { DataTable } from '@/components/common/DataTable';
import { FilterSelect } from '@/components/common/FilterSelect';
import { TableToolbar } from '@/components/common/TableToolbar';
import { MESSAGES } from '@/constants/messages';

const STATUS_TAG: Record<SyncStatus, { color: string; text: string }> = {
  Running: { color: 'processing', text: ATTENDANCE_TEXT.sync.statusRunning },
  Succeeded: { color: 'success', text: ATTENDANCE_TEXT.sync.statusSucceeded },
  Failed: { color: 'error', text: ATTENDANCE_TEXT.sync.statusFailed },
};

const STATUS_OPTIONS = (Object.keys(STATUS_TAG) as SyncStatus[]).map((value) => ({
  value,
  label: STATUS_TAG[value].text,
}));

/** Lịch sử đồng bộ JOB-01 và các lần nhập file Excel dự phòng. */
export function SyncSessionTable() {
  const [status, setStatus] = useState<SyncStatus>();
  const { data, isLoading, error } = useSyncSessions(status);

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
    <>
      <TableToolbar
        filters={
          <FilterSelect<SyncStatus>
            value={status}
            placeholder={ATTENDANCE_TEXT.table.syncStatus}
            options={STATUS_OPTIONS}
            onChange={setStatus}
            style={{ width: 170 }}
          />
        }
      />
      <DataTable<SyncSession>
        rowKey="syncSessionId"
        size="small"
        columns={columns}
        dataSource={data ?? []}
        loading={isLoading}
        scroll={{ x: 'max-content' }}
        locale={{
          emptyText: (
            <Empty
              image={Empty.PRESENTED_IMAGE_SIMPLE}
              description={status ? MESSAGES.common.empty : ATTENDANCE_TEXT.sync.never}
            />
          ),
        }}
      />
    </>
  );
}
