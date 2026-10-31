import { useState } from 'react';
import { Button, Card, Space, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { QueryError } from '@/components/common/QueryError';
import { REQUEST_TYPE } from '@/constants/requestTypes';
import { useRequestInbox } from '@/features/requests/hooks/useRequests';
import type { RequestListItem } from '@/features/requests/types';
import { DATE_TIME_FORMAT, formatDate, toApiDate } from '@/utils/format';
import { ATTENDANCE_TEXT } from '../constants';
import { useAdjustments } from '../hooks/useAttendance';
import type { AdjustmentFilter, AttendanceAdjustment } from '../types';
import { AdjustmentFormModal, type AdjustmentPrefill } from './AdjustmentFormModal';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { DataTable } from '@/components/common/DataTable';
import { AppRangePicker } from '@/components/common/DateInputs';

const text = ATTENDANCE_TEXT.adjust;
const shortTime = (v: string | null) => (v ? v.slice(0, 5) : '—');

const columns: ColumnsType<AttendanceAdjustment> = [
  { title: text.columns.date, dataIndex: 'workDate', render: (v: string) => formatDate(v) },
  {
    title: text.columns.employee,
    key: 'employee',
    render: (_, r) => `${r.employeeCode} - ${r.fullName}`,
  },
  {
    title: text.columns.type,
    dataIndex: 'adjustmentType',
    render: (v: string) => text.types[v] ?? v,
  },
  {
    title: text.columns.times,
    key: 'times',
    render: (_, r) => `${shortTime(r.checkIn)} - ${shortTime(r.checkOut)}`,
  },
  { title: text.columns.reason, dataIndex: 'reason' },
  {
    title: text.columns.recordedAt,
    dataIndex: 'recordedAt',
    render: (v: string) => dayjs(v).format(DATE_TIME_FORMAT),
  },
];

/**
 * GBR-25 / UC07 — tab "Chấm công hộ" của Kế toán: lịch sử chấm bù, nút chấm hộ, và danh sách đơn xin chấm công bù đã duyệt
 * đang chờ để chấm hộ ngay (điền sẵn ngày và đơn).
 */
export function AdjustmentPanel() {
  const [range, setRange] = useState<[Dayjs, Dayjs]>([dayjs().subtract(30, 'day'), dayjs()]);
  const [keyword, setKeyword] = useState('');
  const [page, setPage] = useState(1);
  const [formOpen, setFormOpen] = useState(false);
  const [prefill, setPrefill] = useState<AdjustmentPrefill | null>(null);

  const filter: AdjustmentFilter = {
    fromDate: toApiDate(range[0]),
    toDate: toApiDate(range[1]),
    keyword: keyword || undefined,
    page,
    pageSize: 20,
  };
  const adjustments = useAdjustments(filter);
  const approved = useRequestInbox({
    page: 1,
    pageSize: 20,
    requestType: REQUEST_TYPE.AttendanceCorrection,
    status: 'Approved',
  });

  const openForm = (next: AdjustmentPrefill | null) => {
    setPrefill(next);
    setFormOpen(true);
  };

  const requestColumns: ColumnsType<RequestListItem> = [
    { title: text.approvedRequests.columns.code, dataIndex: 'requestCode' },
    {
      title: text.approvedRequests.columns.title,
      dataIndex: 'title',
      render: (v: string | null) => v ?? '—',
    },
    {
      title: text.approvedRequests.columns.team,
      dataIndex: 'teamName',
      render: (v: string | null) => v ?? '—',
    },
    {
      title: text.approvedRequests.columns.creator,
      dataIndex: 'createdByName',
      render: (v: string | null) => v ?? '—',
    },
    {
      title: text.approvedRequests.columns.date,
      dataIndex: 'targetDate',
      render: (v: string | null) => formatDate(v),
    },
    {
      title: text.approvedRequests.columns.action,
      key: 'action',
      render: (_, r) => (
        <Button
          type="link"
          onClick={() =>
            openForm({ requestId: r.requestId, workDate: r.targetDate, reason: r.title })
          }
        >
          {text.add}
        </Button>
      ),
    },
  ];

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Typography.Text type="secondary">{text.hint}</Typography.Text>

      <Card size="small" title={text.approvedRequests.title}>
        <DataTable<RequestListItem>
          rowKey="requestId"
          size="small"
          columns={requestColumns}
          dataSource={approved.data?.items}
          loading={approved.isFetching}
          locale={{ emptyText: text.approvedRequests.empty }}
          pagination={false}
          scroll={{ x: 'max-content' }}
        />
      </Card>

      <Space wrap>
        <AppRangePicker
          allowClear={false}
          value={range}
          onChange={(v) => {
            if (v?.[0] && v[1]) {
              setRange([v[0], v[1]]);
              setPage(1);
            }
          }}
        />
        <DebouncedSearch
          allowClear
          placeholder={text.search}
          style={{ width: 260 }}
          onSearch={(v) => {
            setKeyword(v.trim());
            setPage(1);
          }}
        />
        <Button type="primary" icon={<PlusOutlined />} onClick={() => openForm(null)}>
          {text.add}
        </Button>
      </Space>

      <QueryError error={adjustments.error} onRetry={() => void adjustments.refetch()} />

      <DataTable<AttendanceAdjustment>
        rowKey="attendanceAdjustmentId"
        size="small"
        columns={columns}
        dataSource={adjustments.data?.items}
        loading={adjustments.isFetching}
        locale={{ emptyText: text.empty }}
        scroll={{ x: 'max-content' }}
        pagination={{
          current: adjustments.data?.page ?? page,
          pageSize: adjustments.data?.pageSize ?? 20,
          total: adjustments.data?.totalItems ?? 0,
          showTotal: (total, [from, to]) => `${from}–${to} / ${total} dòng`,
          onChange: setPage,
        }}
      />

      <AdjustmentFormModal open={formOpen} prefill={prefill} onClose={() => setFormOpen(false)} />
    </Space>
  );
}
