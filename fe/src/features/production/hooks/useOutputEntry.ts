import { useMemo, useState } from 'react';
import { message } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { useTeamLookup } from '@/hooks/useHrLookups';
import { useOrderLineLookup } from '@/hooks/useOrderLookups';
import { fill, formatQty, toApiDate } from '@/utils/format';
import { PRODUCTION_TEXT } from '../constants';
import type { OutputGridRow, OutputLineInput, OutputRole, ShiftCode } from '../types';
import { outputKeyOf } from '../utils';
import { useOutputGrid, useSaveOutput } from './useProduction';

const text = PRODUCTION_TEXT.output;

/**
 * Trạng thái và thao tác của màn sản lượng cuối ca: bộ lọc (ngày, ca, tổ, đơn, dòng đơn), số lượng đang sửa
 * trên lưới của Quản đốc và lưu. Giao diện chỉ lo hiển thị.
 */
export function useOutputEntry(role: OutputRole) {
  const isManager = role === 'manager';
  const [workDate, setWorkDate] = useState<Dayjs>(dayjs());
  const [shift, setShift] = useState<ShiftCode>('Afternoon');
  const [orderId, setOrderId] = useState<number | null>(null);
  const [orderLineId, setOrderLineId] = useState<number | null>(null);
  const [teamId, setTeamId] = useState<number | null>(null);
  const [edits, setEdits] = useState<Record<string, number | null>>({});
  const [reason, setReason] = useState('');
  const [warnings, setWarnings] = useState<string[]>([]);

  const lines = useOrderLineLookup(orderId);
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

  const changedRows = useMemo<OutputLineInput[]>(
    () =>
      rows
        .filter((r) => {
          const edit = edits[outputKeyOf(r)];
          return edit !== undefined && edit !== null && edit !== (r.quantity ?? null);
        })
        .map((r) => ({
          operationId: r.operationId,
          workerId: r.workerId,
          quantity: edits[outputKeyOf(r)] as number,
          rowVer: r.rowVer,
        })),
    [rows, edits],
  );

  const resetEdits = () => {
    setEdits({});
    setWarnings([]);
  };

  const saveRows = (toSave: OutputLineInput[], onDone?: () => void) => {
    if (!query || toSave.length === 0) {
      void message.info(text.nothingChanged);
      return;
    }
    save.mutate(
      { ...query, reason: reason.trim() || undefined, rows: toSave },
      {
        onSuccess: (result) => {
          void message.success(
            fill(text.resultTemplate, result.inserted, result.updated, result.deleted),
          );
          setWarnings(result.warnings);
          setEdits({});
          setReason('');
          onDone?.();
        },
      },
    );
  };

  const handleSave = () => {
    if (isManager && changedRows.length > 0 && reason.trim() === '') {
      void message.error(text.reasonRequired);
      return;
    }
    saveRows(changedRows);
  };

  const lineOptions = (lines.data ?? []).map((l) => ({
    value: l.orderLineId,
    label: `${l.productCode} - ${l.color} - ${l.sizeCode} (đặt ${formatQty(l.orderedQty)})`,
  }));
  const teamOptions = (teams.data ?? [])
    .filter((t) => t.teamType === 'Sewing')
    .map((t) => ({ value: t.teamId, label: t.teamName }));

  const changeQuantity = (row: OutputGridRow, value: number | null) =>
    setEdits((prev) => ({ ...prev, [outputKeyOf(row)]: value }));

  return {
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
    edits,
    reason,
    setReason,
    warnings,
    setWarnings,
    lines,
    teams,
    save,
    query,
    grid,
    rows,
    changedRows,
    resetEdits,
    saveRows,
    handleSave,
    lineOptions,
    teamOptions,
    changeQuantity,
  };
}

export type OutputEntryState = ReturnType<typeof useOutputEntry>;
