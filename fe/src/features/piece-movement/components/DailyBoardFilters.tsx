import { Input, Segmented } from 'antd';
import type { Dayjs } from 'dayjs';
import { AppDatePicker } from '@/components/common/DateInputs';
import { OrderLinePicker, type PickableOrderLine } from '@/components/common/OrderLinePicker';
import { OrderSelect } from '@/components/common/OrderSelect';
import { fill } from '@/utils/format';
import { DAILY_SEARCH_MAX_LENGTH, PM_LABELS } from '../constants';
import { DAILY_STATE, type DailyState, type DailyWorker } from '../utils/dailyBoard';

type DailyBoardFiltersProps = {
  workDate: Dayjs;
  onWorkDateChange: (date: Dayjs) => void;
  disabledDate: (date: Dayjs) => boolean;
  orderId?: number;
  onOrderChange: (orderId: number | undefined) => void;
  lines: readonly PickableOrderLine[];
  linesLoading: boolean;
  orderLineId?: number;
  onOrderLineChange: (orderLineId: number | undefined) => void;
};

/** Chọn việc theo bước: ngày, đơn, rồi mã hàng và màu - size (chỉ hiện khi đã chọn đơn). */
export function DailyBoardFilters({
  workDate,
  onWorkDateChange,
  disabledDate,
  orderId,
  onOrderChange,
  lines,
  linesLoading,
  orderLineId,
  onOrderLineChange,
}: DailyBoardFiltersProps) {
  return (
    <div className="daily-filters">
      <AppDatePicker
        aria-label={PM_LABELS.workDate}
        allowClear={false}
        value={workDate}
        disabledDate={disabledDate}
        onChange={(d) => d && onWorkDateChange(d)}
      />
      <OrderSelect
        value={orderId}
        onChange={onOrderChange}
        placeholder={PM_LABELS.selectOrder}
        width={300}
      />
      {orderId !== undefined && (
        <div className="daily-filters__line">
          <OrderLinePicker
            lines={lines}
            value={orderLineId}
            onChange={onOrderLineChange}
            loading={linesLoading}
          />
        </div>
      )}
    </div>
  );
}

type DailyWorkerFiltersProps = {
  workers: readonly DailyWorker[];
  keyword: string;
  onKeywordChange: (keyword: string) => void;
  state: DailyState;
  onStateChange: (state: DailyState) => void;
};

/** Lọc trong danh sách công nhân; tình trạng việc là nút bấm một chạm kèm số người. */
export function DailyWorkerFilters({
  workers,
  keyword,
  onKeywordChange,
  state,
  onStateChange,
}: DailyWorkerFiltersProps) {
  const idle = workers.filter((w) => w.operationCount === 0).length;
  return (
    <div className="daily-filters">
      <Input.Search
        allowClear
        className="daily-filters__search"
        placeholder={PM_LABELS.searchWorker}
        maxLength={DAILY_SEARCH_MAX_LENGTH}
        value={keyword}
        onChange={(e) => onKeywordChange(e.target.value)}
      />
      <Segmented<DailyState>
        aria-label={PM_LABELS.stateLabel}
        value={state}
        onChange={onStateChange}
        options={[
          { value: DAILY_STATE.ALL, label: fill(PM_LABELS.stateAll, workers.length) },
          { value: DAILY_STATE.IDLE, label: fill(PM_LABELS.stateIdle, idle) },
          { value: DAILY_STATE.BUSY, label: fill(PM_LABELS.stateBusy, workers.length - idle) },
        ]}
      />
    </div>
  );
}
