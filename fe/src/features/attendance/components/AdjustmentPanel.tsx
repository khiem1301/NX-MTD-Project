import { useState } from 'react';
import { Button, Card, Space } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { IconAction } from '@/components/common/IconAction';
import { QueryError } from '@/components/common/QueryError';
import { DEFAULT_PAGE_SIZE } from '@/constants/pagination';
import { REQUEST_TYPE } from '@/constants/requestTypes';
import { useRequestInbox } from '@/hooks/useRequests';
import type { RequestListItem } from '@/types/request';
import { DATE_TIME_FORMAT, formatDate, toApiDate } from '@/utils/format';
import { ATTENDANCE_TEXT } from '../constants';
import { TABLE_SORT, type ServerSort, type SortablePagination } from '@/utils/tableSort';
import { useAdjustments } from '../hooks/useAttendance';
import type { AdjustmentFilter, AttendanceAdjustment } from '../types';
import { AdjustmentFormModal, type AdjustmentPrefill } from './AdjustmentFormModal';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { DataTable } from '@/components/common/DataTable';
import { AppDateRangeFields } from '@/components/common/DateInputs';
import { HintTip } from '@/components/common/HintTip';

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
export function AdjustmentPanel({ presetRequestId = null }: { presetRequestId?: number | null }) {
  // 60 ngày: đơn xin chấm bù demo/thực tế thường có ngày công cũ hơn 30 ngày.
  const [range, setRange] = useState<[Dayjs, Dayjs]>([dayjs().subtract(60, 'day'), dayjs()]);
  const [keyword, setKeyword] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const [sort, setSort] = useState<ServerSort>({});
  const [formOpen, setFormOpen] = useState(false);
  const [prefill, setPrefill] = useState<AdjustmentPrefill | null>(null);
  const [presetDone, setPresetDone] = useState(false);

  /** Sau khi lưu: đảm bảo khoảng lọc chứa ngày công vừa chấm, rồi về trang 1. */
  const includeWorkDateInRange = (workDate: string) => {
    const day = dayjs(workDate);
    setRange(([from, to]) => {
      const nextFrom = day.isBefore(from, 'day') ? day : from;
      const nextTo = day.isAfter(to, 'day') ? day : to;
      return [nextFrom, nextTo];
    });
    setPage(1);
  };

  const filter: AdjustmentFilter = {
    fromDate: toApiDate(range[0]),
    toDate: toApiDate(range[1]),
    keyword: keyword || undefined,
    page,
    pageSize,
    ...sort,
  };
  const adjustments = useAdjustments(filter);
  const approved = useRequestInbox({
    page: 1,
    pageSize: 20,
    requestType: REQUEST_TYPE.AttendanceCorrection,
    status: 'Approved',
  });

  const openForm = (next: AdjustmentPrefill | null) => {
    setPresetDone(true);
    setPrefill(next);
    setFormOpen(true);
  };
  const closeForm = () => {
    setPresetDone(true);
    setFormOpen(false);
  };

  // Mở từ nút "Xử lý ngay" của đơn: mở sẵn form chấm hộ của đơn đó khi danh sách đơn đã duyệt tải xong.
  const presetRequest = presetDone
    ? undefined
    : approved.data?.items.find((r) => r.requestId === presetRequestId);
  const formPrefill: AdjustmentPrefill | null = presetRequest
    ? {
        requestId: presetRequest.requestId,
        workDate: presetRequest.targetDate,
        reason: presetRequest.title,
      }
    : prefill;

  const requestColumns: ColumnsType<RequestListItem> = [
    { title: text.approvedRequests.columns.code, dataIndex: 'requestCode' },
    {
      title: text.approvedRequests.columns.title,
      dataIndex: 'title',
      render: (v: string | null) => v ?? '—',
    },
    {
      title: text.approvedRequests.columns.team,
      dataIndex: 'teamCode',
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
        <IconAction
          title={text.add}
          icon={<PlusOutlined />}
          onClick={() =>
            openForm({ requestId: r.requestId, workDate: r.targetDate, reason: r.title })
          }
        />
      ),
    },
  ];

  const pagination: SortablePagination = {
    current: adjustments.data?.page ?? page,
    pageSize,
    total: adjustments.data?.totalItems ?? 0,
    onChange: (nextPage, nextSize) => {
      setPage(nextSize === pageSize ? nextPage : 1);
      setPageSize(nextSize);
    },
    [TABLE_SORT]: {
      sort,
      onChange: (next) => {
        setSort(next);
        setPage(1);
      },
    },
  };

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <HintTip text={text.hint} />

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
        <AppDateRangeFields
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
        pagination={pagination}
      />

      <AdjustmentFormModal
        open={formOpen || presetRequest !== undefined}
        prefill={formPrefill}
        onClose={closeForm}
        onSaved={includeWorkDateInRange}
      />
    </Space>
  );
}
