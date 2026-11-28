import { useMemo, useState } from 'react';
import { Alert, Empty, Space } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { STAT_CARD_CLASS } from '@/components/common/cardTable';
import { DataTable } from '@/components/common/DataTable';
import { useWorkDateRange } from '@/hooks/useLookups';
import { QueryError } from '@/components/common/QueryError';
import { useAuth } from '@/hooks/useAuth';
import { useTeamMemberLookup } from '@/hooks/useHrLookups';
import { useOrderLineLookup } from '@/hooks/useOrderLookups';
import { toApiDate } from '@/utils/format';
import { PM_LABELS } from '../constants';
import { useBoard, useDailyBoard } from '../hooks/usePieceMovement';
import {
  buildDailyWorkers,
  DAILY_STATE,
  filterDailyWorkers,
  type DailyState,
  type DailyWorker,
} from '../utils/dailyBoard';
import { dailyBoardColumns } from './dailyBoardColumns';
import { DailyBoardFilters, DailyWorkerFilters } from './DailyBoardFilters';
import './dailyBoard.css';
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

  const columns = dailyBoardColumns(setEditingId, canEdit && isToday);
  // Đơn chưa chia cho tổ đã có cảnh báo riêng, không nhắc chọn tiếp.
  const hasNoBoard = orderId !== undefined && orderLines.isSuccess && lines.length === 0;

  return (
    <Space direction="vertical" size={12} style={{ width: '100%' }}>
      <DailyBoardFilters
        workDate={workDate}
        onWorkDateChange={setWorkDate}
        disabledDate={disabledWorkDate}
        orderId={orderId}
        onOrderChange={(v) => {
          setOrderId(v);
          setOrderLineId(undefined);
        }}
        lines={lines}
        linesLoading={orderLines.isLoading}
        orderLineId={orderLineId}
        onOrderLineChange={setOrderLineId}
      />
      {!isToday && <Alert type="info" showIcon message={PM_LABELS.pastDayReadOnly} />}
      <QueryError error={daily.error} onRetry={() => void daily.refetch()} />
      <QueryError error={members.error} onRetry={() => void members.refetch()} />
      <QueryError error={orderLines.error} onRetry={() => void orderLines.refetch()} />
      {hasNoBoard && <Alert type="warning" showIcon message={PM_LABELS.noBoard} />}
      {orderLineId === undefined ? (
        !hasNoBoard && (
          <Empty
            image={Empty.PRESENTED_IMAGE_SIMPLE}
            description={orderId === undefined ? PM_LABELS.selectOrder : PM_LABELS.selectOrderLine}
          />
        )
      ) : (
        <>
          <DailyWorkerFilters
            workers={workers}
            keyword={keyword}
            onKeywordChange={setKeyword}
            state={state}
            onStateChange={setState}
          />
          <DataTable<DailyWorker>
            className={STAT_CARD_CLASS}
            rowKey="workerId"
            columns={columns}
            dataSource={visible}
            loading={daily.isFetching || members.isLoading}
            locale={{ emptyText: PM_LABELS.noWorkers }}
          />
        </>
      )}
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
