import { Select, Space } from 'antd';
import dayjs from 'dayjs';
import { AppDatePicker } from '@/components/common/DateInputs';
import { OrderLinePicker } from '@/components/common/OrderLinePicker';
import { useWorkDateRange } from '@/hooks/useLookups';
import { OrderSelect } from '@/components/common/OrderSelect';
import { OUTPUT_SHIFT_FILTER_OPTIONS, PRODUCTION_TEXT } from '../constants';
import type { OutputEntryState } from '../hooks/useOutputEntry';
import type { ShiftFilter } from '../types';

type OutputFilterBarProps = { entry: OutputEntryState; isManager: boolean };

/** Bộ lọc của lưới sản lượng: ngày, ca, tổ (Quản đốc), đơn hàng, mã hàng, màu - size (DEV-ALL-68). Đổi bộ lọc thì ẩn cảnh báo của lần lưu trước. */
export function OutputFilterBar({ entry, isManager }: OutputFilterBarProps) {
  const {
    workDate,
    setWorkDate,
    shift,
    setShift,
    teamId,
    setTeamId,
    orderId,
    setOrderId,
    orderLineId,
    setOrderLineId,
    teams,
    lines,
    teamOptions,
    resetWarnings,
  } = entry;
  const disabledWorkDate = useWorkDateRange(orderId ?? undefined);
  return (
    <Space wrap>
      <AppDatePicker
        allowClear={false}
        value={workDate}
        disabledDate={disabledWorkDate}
        onChange={(v) => {
          setWorkDate(v ?? dayjs());
          resetWarnings();
        }}
      />
      <Select<ShiftFilter>
        style={{ width: 130 }}
        value={shift}
        options={OUTPUT_SHIFT_FILTER_OPTIONS}
        onChange={(v) => {
          setShift(v);
          resetWarnings();
        }}
      />
      {isManager && (
        <Select<number>
          style={{ width: 180 }}
          placeholder={PRODUCTION_TEXT.selectTeam}
          value={teamId ?? undefined}
          options={teamOptions}
          loading={teams.isLoading}
          onChange={(v) => {
            setTeamId(v);
            // Đơn, mã hàng, màu - size phải là phần đã chia cho tổ vừa chọn.
            setOrderId(null);
            setOrderLineId(null);
            resetWarnings();
          }}
        />
      )}
      <OrderSelect
        value={orderId ?? undefined}
        sewingTeamId={isManager ? (teamId ?? undefined) : undefined}
        disabled={isManager && teamId === null}
        placeholder={
          isManager && teamId === null
            ? PRODUCTION_TEXT.selectTeamFirst
            : PRODUCTION_TEXT.selectOrder
        }
        onChange={(v) => {
          setOrderId(v ?? null);
          setOrderLineId(null);
          resetWarnings();
        }}
      />
      <OrderLinePicker
        lines={lines.data ?? []}
        value={orderLineId ?? undefined}
        disabled={orderId === null}
        disabledPlaceholder={PRODUCTION_TEXT.selectFirst}
        loading={lines.isLoading}
        onChange={(v) => {
          setOrderLineId(v ?? null);
          resetWarnings();
        }}
      />
    </Space>
  );
}
