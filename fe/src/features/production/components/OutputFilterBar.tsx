import { Select, Space } from 'antd';
import dayjs from 'dayjs';
import { AppDatePicker } from '@/components/common/DateInputs';
import { OrderSelect } from '@/components/common/OrderSelect';
import { PRODUCTION_TEXT, SHIFT_OPTIONS } from '../constants';
import type { OutputEntryState } from '../hooks/useOutputEntry';
import type { ShiftCode } from '../types';

type OutputFilterBarProps = { entry: OutputEntryState; isManager: boolean };

/** Bộ lọc của lưới sản lượng: ngày, ca, tổ (Quản đốc), đơn hàng và dòng đơn. Đổi bộ lọc thì bỏ các số đang sửa. */
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
    lineOptions,
    resetEdits,
  } = entry;
  return (
    <Space wrap>
      <AppDatePicker
        allowClear={false}
        value={workDate}
        disabledDate={(d) => d.isAfter(dayjs(), 'day')}
        onChange={(v) => {
          setWorkDate(v ?? dayjs());
          resetEdits();
        }}
      />
      <Select<ShiftCode>
        style={{ width: 130 }}
        value={shift}
        options={SHIFT_OPTIONS}
        onChange={(v) => {
          setShift(v);
          resetEdits();
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
            resetEdits();
          }}
        />
      )}
      <OrderSelect
        value={orderId ?? undefined}
        placeholder={PRODUCTION_TEXT.selectOrder}
        onChange={(v) => {
          setOrderId(v ?? null);
          setOrderLineId(null);
          resetEdits();
        }}
      />
      <Select<number>
        style={{ width: 300 }}
        showSearch
        optionFilterProp="label"
        placeholder={
          orderId === null ? PRODUCTION_TEXT.selectFirst : PRODUCTION_TEXT.selectOrderLine
        }
        disabled={orderId === null}
        loading={lines.isLoading}
        value={orderLineId ?? undefined}
        options={lineOptions}
        onChange={(v) => {
          setOrderLineId(v);
          resetEdits();
        }}
      />
    </Space>
  );
}
