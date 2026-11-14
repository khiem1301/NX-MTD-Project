import { useMemo, useState } from 'react';
import { message } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { useTeamLookup } from '@/hooks/useHrLookups';
import { useOrderLineLookup } from '@/hooks/useOrderLookups';
import { fill, toApiDate } from '@/utils/format';
import { PRODUCTION_TEXT } from '../constants';
import type { OutputLineInput, OutputRole, ShiftFilter } from '../types';
import { useOutputGrid, useSaveOutput } from './useOutput';

const text = PRODUCTION_TEXT.output;

/**
 * Trạng thái và thao tác của màn sản lượng cuối ca (tổ trưởng) và Sửa sản lượng sau hạn (Quản đốc, chọn thêm tổ may):
 * bộ lọc (ngày, ca / cả ngày, tổ, đơn, mã hàng, màu - size) và lưu sản lượng của từng công nhân.
 * Cả ngày chỉ xem tổng các ca; nhập/sửa vẫn theo từng ca.
 */
export function useOutputEntry(role: OutputRole) {
  const isManager = role === 'manager';
  const [workDate, setWorkDate] = useState<Dayjs>(dayjs());
  const [shift, setShift] = useState<ShiftFilter>('Afternoon');
  const [orderId, setOrderId] = useState<number | null>(null);
  const [orderLineId, setOrderLineId] = useState<number | null>(null);
  const [teamId, setTeamId] = useState<number | null>(null);
  const [warnings, setWarnings] = useState<string[]>([]);

  const lines = useOrderLineLookup(orderId, isManager ? (teamId ?? undefined) : undefined);
  const teams = useTeamLookup();
  const save = useSaveOutput(role);
  const isAllDay = shift === 'AllDay';

  const query =
    orderLineId !== null && (!isManager || teamId !== null)
      ? {
          workDate: toApiDate(workDate),
          orderLineId,
          sewingTeamId: isManager ? (teamId ?? undefined) : undefined,
          ...(isAllDay ? { allDay: true as const } : { shiftCode: shift }),
        }
      : null;
  const grid = useOutputGrid(role, query);
  const rows = useMemo(() => grid.data?.rows ?? [], [grid.data]);

  const resetWarnings = () => setWarnings([]);

  /** Lưu sản lượng của một công nhân; Quản đốc gửi kèm lý do sửa. Trả về Promise để UI đóng popup sau khi thành công. */
  const saveRows = async (
    toSave: OutputLineInput[],
    onDone?: () => void,
    transferExcess = false,
    reason?: string,
  ) => {
    // Cả ngày chỉ xem — không lưu; sau `isAllDay` thì `shift` đã hẹp thành ShiftCode.
    if (!query || isAllDay || toSave.length === 0) {
      void message.info(text.nothingChanged);
      return;
    }
    const result = await save.mutateAsync({
      workDate: query.workDate,
      shiftCode: shift,
      orderLineId: query.orderLineId,
      sewingTeamId: query.sewingTeamId,
      reason: reason?.trim() || undefined,
      rows: toSave,
      transferExcess,
    });
    void message.success(
      fill(text.resultTemplate, result.inserted, result.updated, result.deleted),
    );
    setWarnings(result.warnings);
    onDone?.();
  };

  const teamOptions = (teams.data ?? [])
    .filter((t) => t.teamType === 'Sewing')
    .map((t) => ({ value: t.teamId, label: t.teamName }));

  return {
    isManager,
    workDate,
    setWorkDate,
    shift,
    setShift,
    isAllDay,
    orderId,
    setOrderId,
    setOrderLineId,
    orderLineId,
    teamId,
    setTeamId,
    warnings,
    setWarnings,
    lines,
    teams,
    save,
    query,
    grid,
    rows,
    resetWarnings,
    saveRows,
    teamOptions,
  };
}

export type OutputEntryState = ReturnType<typeof useOutputEntry>;
