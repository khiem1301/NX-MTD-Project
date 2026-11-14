import { useMemo, useState } from 'react';
import { Alert, Input, Select, Space } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { AppDatePicker } from '@/components/common/DateInputs';
import { DataTable } from '@/components/common/DataTable';
import { OrderLinePicker } from '@/components/common/OrderLinePicker';
import { useWorkDateRange } from '@/hooks/useLookups';
import { OrderSelect } from '@/components/common/OrderSelect';
import { QueryError } from '@/components/common/QueryError';
import { useAuth } from '@/hooks/useAuth';
import { useTeamMemberLookup } from '@/hooks/useHrLookups';
import { useOrderLineLookup } from '@/hooks/useOrderLookups';
import { fill, toApiDate } from '@/utils/format';
import { DAILY_SEARCH_MAX_LENGTH, PM_LABELS } from '../constants';
import { useBoard, useDailyBoard } from '../hooks/usePieceMovement';
import {
  buildDailyWorkers,
  DAILY_STATE,
  filterDailyWorkers,
  type DailyState,
  type DailyWorker,
} from '../utils/dailyBoard';
import { dailyBoardColumns } from './dailyBoardColumns';
import { DailyWorkerModal } from './DailyWorkerModal';

/**
 * Giao sản lượng công đoạn theo ngày (bố cục như màn Sản lượng cuối ca): chọn ngày, đơn, dòng đơn; bảng công nhân của tổ kèm tồn từ
 * hôm trước, số giao trong ngày, đã làm, chưa làm hết (chuyển sang hôm sau). Nút Giao việc mở khung nhập số lần giao từng công đoạn.
 */
export function DailyAllocationBoard({ canEdit }: { canEdit: boolean }) {
  const { user } = useAuth();
  const [workDate, setWorkDate] = useState<Dayjs>(dayjs());
  const [orderId, setOrderId] = useState<number>();
  const [orderLineId, setOrderLineId] = useState<number>();
  const [keyword, setKeyword] = useState('');
  const [state, setState] = useState<DailyState>(DAILY_STATE.ALL);
  const [editingId, setEditingId] = useState<number | null>(null);
  const apiDate = toApiDate(workDate);
  // Chỉ giao và sửa trong ngày hôm nay; sang ngày sau chỉ xem (BE chặn cùng quy tắc).
  const isToday = workDate.isSame(dayjs(), 'day');
  // Ô mã hàng, màu - size lấy các dòng đơn đã chia cho tổ; bảng giao việc chỉ tải đúng dòng đơn đang chọn.
  const orderLines = useOrderLineLookup(orderId ?? null);
  const disabledWorkDate = useWorkDateRange(orderId);
  const daily = useDailyBoard(apiDate, orderId, orderLineId);
  // Công đoạn để thêm ngoài phân công: chỉ cần khi đã chọn dòng đơn.
  const board = useBoard(orderLineId === undefined ? undefined : orderId);
  const members = useTeamMemberLookup(user?.teamId ?? null);

  const workerOptions = useMemo(
    () =>
      (members.data ?? [])
        .filter((m) => m.role === 'Worker')
        .map((m) => ({ value: m.employeeId, label: `${m.employeeCode} - ${m.fullName}` })),
    [members.data],
  );
  const rows = useMemo(() => daily.data?.rows ?? [], [daily.data]);
  // Phải chọn đủ đơn, mã hàng, màu - size mới giao được (DEV-ALL-68).
  const workers = useMemo(
    () => (orderLineId === undefined ? [] : buildDailyWorkers(rows, workerOptions, orderLineId)),
    [rows, workerOptions, orderLineId],
  );
  const visible = filterDailyWorkers(workers, keyword, state);
  const editing = workers.find((w) => w.workerId === editingId) ?? null;
  const lines = orderLines.data ?? [];
  const operations = (board.data?.operations ?? []).filter((o) => o.orderLineId === orderLineId);

  const columns = dailyBoardColumns(setEditingId);

  return (
    <Space direction="vertical" size={12} style={{ width: '100%' }}>
      <Space wrap>
        <AppDatePicker
          allowClear={false}
          aria-label={PM_LABELS.workDate}
          value={workDate}
          disabledDate={disabledWorkDate}
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
        <OrderLinePicker
          lines={lines}
          value={orderLineId}
          onChange={setOrderLineId}
          disabled={orderId === undefined}
          disabledPlaceholder={PM_LABELS.selectFirst}
          loading={orderLines.isLoading}
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
      </Space>
      {!isToday && <Alert type="info" showIcon message={PM_LABELS.pastDayReadOnly} />}
      <QueryError error={daily.error} onRetry={() => void daily.refetch()} />
      <QueryError error={members.error} onRetry={() => void members.refetch()} />
      <QueryError error={orderLines.error} onRetry={() => void orderLines.refetch()} />
      {orderId !== undefined && orderLines.isSuccess && lines.length === 0 && (
        <Alert type="warning" showIcon message={PM_LABELS.noBoard} />
      )}
      <DataTable<DailyWorker>
        rowKey="workerId"
        columns={columns}
        dataSource={visible}
        loading={daily.isFetching || members.isLoading}
        locale={{
          emptyText:
            orderId === undefined
              ? PM_LABELS.selectOrder
              : orderLineId === undefined
                ? PM_LABELS.selectOrderLine
                : PM_LABELS.noWorkers,
        }}
      />
      {editing && (
        <DailyWorkerModal
          key={`${editing.workerId}-${apiDate}`}
          worker={editing}
          workDate={apiDate}
          canEdit={canEdit && isToday}
          operations={operations}
          onClose={() => setEditingId(null)}
        />
      )}
    </Space>
  );
}
