import { useMemo, useState } from 'react';
import { message } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { useTeamLookup } from '@/hooks/useHrLookups';
import { useOrderLineLookup } from '@/hooks/useOrderLookups';
import { fill, toApiDate } from '@/utils/format';
import { PRODUCTION_TEXT } from '../constants';
import type { OutputLineInput, OutputRole, ShiftCode } from '../types';
import { useOutputGrid, useSaveOutput } from './useOutput';

const text = PRODUCTION_TEXT.output;

/**
 * Trạng thái và thao tác của màn sản lượng cuối ca (tổ trưởng) và Sửa sản lượng sau hạn (Quản đốc, chọn thêm tổ may):
 * bộ lọc (ngày, ca, tổ, đơn, mã hàng, màu - size) và lưu sản lượng của từng công nhân. Giao diện chỉ lo hiển thị.
 */
export function useOutputEntry(role: OutputRole) {
  const isManager = role === 'manager';
  const [workDate, setWorkDate] = useState<Dayjs>(dayjs());
  const [shift, setShift] = useState<ShiftCode>('Afternoon');
  const [orderId, setOrderId] = useState<number | null>(null);
  const [orderLineId, setOrderLineId] = useState<number | null>(null);
  const [teamId, setTeamId] = useState<number | null>(null);
  const [warnings, setWarnings] = useState<string[]>([]);

  const lines = useOrderLineLookup(orderId, isManager ? (teamId ?? undefined) : undefined);
  const teams = useTeamLookup();
  const save = useSaveOutput(role);

  const query =
    orderLineId !== null && (!isManager || teamId !== null)
      ? {
          workDate: toApiDate(workDate),
          shiftCode: shift,
          orderLineId,
          sewingTeamId: isManager ? (teamId ?? undefined) : undefined,
        }
      : null;
  const grid = useOutputGrid(role, query);
  const rows = useMemo(() => grid.data?.rows ?? [], [grid.data]);

  const resetWarnings = () => setWarnings([]);

  /** Lưu sản lượng của một công nhân; Quản đốc gửi kèm lý do sửa. */
  const saveRows = (
    toSave: OutputLineInput[],
    onDone?: () => void,
    transferExcess = false,
    reason?: string,
  ) => {
    if (!query || toSave.length === 0) {
      void message.info(text.nothingChanged);
      return;
    }
    save.mutate(
      { ...query, reason: reason?.trim() || undefined, rows: toSave, transferExcess },
      {
        onSuccess: (result) => {
          void message.success(
            fill(text.resultTemplate, result.inserted, result.updated, result.deleted),
          );
          setWarnings(result.warnings);
          onDone?.();
        },
      },
    );
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
