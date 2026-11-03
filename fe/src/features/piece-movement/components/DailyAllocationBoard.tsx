import { useMemo, useState } from 'react';
import { EditOutlined, RollbackOutlined } from '@ant-design/icons';
import { Alert, Button, Input, Select, Space, Tag, Typography, type TableColumnsType } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { AppDatePicker } from '@/components/common/DateInputs';
import { DataTable } from '@/components/common/DataTable';
import { IconAction } from '@/components/common/IconAction';
import { OrderSelect } from '@/components/common/OrderSelect';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { useAuth } from '@/hooks/useAuth';
import { useTeamMemberLookup } from '@/hooks/useHrLookups';
import { fill, toApiDate } from '@/utils/format';
import { DAILY_SEARCH_MAX_LENGTH, PM_LABELS } from '../constants';
import { useBoard, useDailyBoard } from '../hooks/usePieceMovement';
import {
  buildDailyWorkers,
  DAILY_STATE,
  dailyOrderLines,
  filterDailyWorkers,
  type DailyState,
  type DailyWorker,
} from '../utils/dailyBoard';
import { DailyWorkerModal } from './DailyWorkerModal';
import { RetrieveRemainingModal } from './RetrieveRemainingModal';

/**
 * Giao sản lượng công đoạn theo ngày (bố cục như màn Sản lượng cuối ca): chọn ngày, đơn, dòng đơn; bảng công nhân của tổ kèm số đã giao,
 * đã làm, chưa làm hết trong ngày. Nút Giao việc mở khung nhập số lần giao từng công đoạn; Thu hồi cuối ngày thu phần chưa làm hết.
 */
export function DailyAllocationBoard({ canEdit }: { canEdit: boolean }) {
  const { user } = useAuth();
  const [workDate, setWorkDate] = useState<Dayjs>(dayjs());
  const [orderId, setOrderId] = useState<number>();
  const [orderLineId, setOrderLineId] = useState<number>();
  const [keyword, setKeyword] = useState('');
  const [state, setState] = useState<DailyState>(DAILY_STATE.ALL);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [retrieving, setRetrieving] = useState(false);
  const apiDate = toApiDate(workDate);
  const daily = useDailyBoard(apiDate, orderId);
  const board = useBoard(orderId);
  const members = useTeamMemberLookup(user?.teamId ?? null);

  const workerOptions = useMemo(
    () =>
      (members.data ?? [])
        .filter((m) => m.role === 'Worker')
        .map((m) => ({ value: m.employeeId, label: `${m.employeeCode} - ${m.fullName}` })),
    [members.data],
  );
  const rows = useMemo(() => daily.data?.rows ?? [], [daily.data]);
  const workers = useMemo(
    () => buildDailyWorkers(rows, workerOptions, orderLineId),
    [rows, workerOptions, orderLineId],
  );
  const visible = filterDailyWorkers(workers, keyword, state);
  const editing = workers.find((w) => w.workerId === editingId) ?? null;
  const lines = useMemo(() => dailyOrderLines(rows), [rows]);
  const operations = (board.data?.operations ?? []).filter(
    (o) => orderLineId === undefined || o.orderLineId === orderLineId,
  );

  const columns: TableColumnsType<DailyWorker> = [
    {
      title: PM_LABELS.worker,
      key: 'worker',
      render: (_, w) => <Typography.Text strong>{w.label}</Typography.Text>,
    },
    {
      title: PM_LABELS.operationCount,
      key: 'operationCount',
      width: 130,
      align: 'center',
      render: (_, w) => (
        <Tag color={w.operationCount === 0 ? 'orange' : 'default'} style={{ marginInlineEnd: 0 }}>
          {w.operationCount}
        </Tag>
      ),
    },
    { title: PM_LABELS.allocated, dataIndex: 'allocatedQty', width: 150, align: 'right' },
    { title: PM_LABELS.output, dataIndex: 'outputQty', width: 100, align: 'right' },
    { title: PM_LABELS.remaining, dataIndex: 'remainingQty', width: 130, align: 'right' },
    {
      title: MESSAGES.common.actions,
      key: 'actions',
      render: (_, w) => (
        <IconAction
          title={PM_LABELS.giveWork}
          icon={<EditOutlined />}
          onClick={() => setEditingId(w.workerId)}
        />
      ),
    },
  ];

  return (
    <Space direction="vertical" size={12} style={{ width: '100%' }}>
      <Space wrap>
        <AppDatePicker
          allowClear={false}
          aria-label={PM_LABELS.workDate}
          value={workDate}
          disabledDate={(d) => d.isAfter(dayjs(), 'day')}
          onChange={(v) => setWorkDate(v ?? dayjs())}
        />
        <OrderSelect
          value={orderId}
          onChange={(v) => {
            setOrderId(v);
            setOrderLineId(undefined);
          }}
          placeholder={PM_LABELS.selectOrder}
          width={300}
        />
        <Select<number>
          allowClear
          showSearch
          optionFilterProp="label"
          style={{ width: 280 }}
          placeholder={orderId === undefined ? PM_LABELS.selectFirst : PM_LABELS.allOrderLines}
          disabled={orderId === undefined}
          loading={daily.isLoading}
          value={orderLineId}
          onChange={setOrderLineId}
          options={lines.map((l) => ({
            value: l.orderLineId,
            label: `${l.productCode} - ${l.color} - ${l.sizeCode}`,
          }))}
        />
        <Input.Search
          allowClear
          style={{ width: 220 }}
          placeholder={PM_LABELS.searchWorker}
          maxLength={DAILY_SEARCH_MAX_LENGTH}
          value={keyword}
          onChange={(e) => setKeyword(e.target.value)}
        />
        <Select<DailyState>
          style={{ width: 200 }}
          aria-label={PM_LABELS.stateLabel}
          value={state}
          onChange={setState}
          options={[
            { value: DAILY_STATE.ALL, label: fill(PM_LABELS.stateAll, workers.length) },
            {
              value: DAILY_STATE.IDLE,
              label: fill(
                PM_LABELS.stateIdle,
                workers.filter((w) => w.operationCount === 0).length,
              ),
            },
            {
              value: DAILY_STATE.BUSY,
              label: fill(PM_LABELS.stateBusy, workers.filter((w) => w.operationCount > 0).length),
            },
          ]}
        />
        {canEdit && (
          <Button
            icon={<RollbackOutlined />}
            disabled={orderId === undefined}
            onClick={() => setRetrieving(true)}
          >
            {PM_LABELS.retrieveRemaining}
          </Button>
        )}
      </Space>
      <QueryError error={daily.error} onRetry={() => void daily.refetch()} />
      <QueryError error={members.error} onRetry={() => void members.refetch()} />
      {orderId !== undefined && daily.isSuccess && lines.length === 0 && (
        <Alert type="warning" showIcon message={PM_LABELS.noBoard} />
      )}
      <DataTable<DailyWorker>
        rowKey="workerId"
        columns={columns}
        dataSource={visible}
        loading={daily.isFetching || members.isLoading}
        locale={{ emptyText: orderId === undefined ? PM_LABELS.selectOrder : PM_LABELS.noWorkers }}
      />
      {editing && (
        <DailyWorkerModal
          key={`${editing.workerId}-${apiDate}`}
          worker={editing}
          workDate={apiDate}
          canEdit={canEdit}
          operations={operations}
          onClose={() => setEditingId(null)}
        />
      )}
      {retrieving && (
        <RetrieveRemainingModal
          workDate={apiDate}
          workerOptions={workerOptions}
          onClose={() => setRetrieving(false)}
        />
      )}
    </Space>
  );
}
