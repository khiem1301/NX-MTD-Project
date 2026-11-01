import { useMemo, useState } from 'react';
import { message } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { useAuth } from '@/hooks/useAuth';
import { useTeamLookup, useTeamMemberLookup } from '@/hooks/useHrLookups';
import { useOrderLineLookup } from '@/hooks/useOrderLookups';
import { fill, formatQty, toApiDate } from '@/utils/format';
import { PRODUCTION_TEXT } from '../constants';
import type { OutputGridRow, OutputLineInput, OutputRole, ShiftCode } from '../types';
import { outputKeyOf } from '../utils';
import { useAssignableOperations, useOutputGrid, useSaveOutput } from './useProduction';

const text = PRODUCTION_TEXT.output;

/**
 * Trạng thái và thao tác của lưới sản lượng cuối ca: bộ lọc (ngày, ca, tổ, đơn, dòng đơn), số lượng đang sửa,
 * dòng ngoài phân công vừa thêm (GBR-20) và lưu. Giao diện chỉ lo hiển thị.
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
  // Dòng ngoài phân công tổ trưởng vừa thêm, chưa lưu (GBR-20); lưu xong thì lưới tải lại từ BE.
  const [extraRows, setExtraRows] = useState<OutputGridRow[]>([]);
  const [extraOperationId, setExtraOperationId] = useState<number | undefined>(undefined);
  const [extraWorkerId, setExtraWorkerId] = useState<number | undefined>(undefined);

  const { user } = useAuth();
  const operations = useAssignableOperations(isManager ? null : orderId);
  const members = useTeamMemberLookup(isManager ? null : (user?.teamId ?? null));
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
  const rows = useMemo(() => {
    const gridRows = grid.data?.rows ?? [];
    return [
      ...gridRows,
      ...extraRows.filter((e) => !gridRows.some((g) => outputKeyOf(g) === outputKeyOf(e))),
    ];
  }, [grid.data, extraRows]);

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
    setExtraRows([]);
  };

  const addExtraRow = () => {
    const operation = operations.data?.find((o) => o.operationId === extraOperationId);
    const worker = members.data?.find((m) => m.employeeId === extraWorkerId);
    if (!operation || !worker) return;
    setExtraRows((prev) => [
      ...prev,
      {
        operationId: operation.operationId,
        operationCode: operation.operationCode,
        operationName: operation.operationName,
        workerId: worker.employeeId,
        workerCode: worker.employeeCode,
        workerName: worker.fullName,
        outputRecordId: null,
        quantity: null,
        unitRate: null,
        rateApproved: true,
        isOutsideAssignment: true,
        capQty: 0,
        reportedQty: 0,
        noAttendanceWarning: false,
        canEdit: true,
        editDeadline: null,
        rowVer: null,
      },
    ]);
    setExtraOperationId(undefined);
    setExtraWorkerId(undefined);
  };

  const handleSave = () => {
    if (!query || changedRows.length === 0) {
      void message.info(text.nothingChanged);
      return;
    }
    if (isManager && reason.trim() === '') {
      void message.error(text.reasonRequired);
      return;
    }
    save.mutate(
      { ...query, reason: reason.trim() || undefined, rows: changedRows },
      {
        onSuccess: (result) => {
          void message.success(
            fill(text.resultTemplate, result.inserted, result.updated, result.deleted),
          );
          setWarnings(result.warnings);
          setEdits({});
          setExtraRows([]);
          setReason('');
        },
      },
    );
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

  const removeExtraRow = (row: OutputGridRow) => {
    setExtraRows((prev) => prev.filter((e) => outputKeyOf(e) !== outputKeyOf(row)));
    setEdits((prev) => {
      const { [outputKeyOf(row)]: _removed, ...rest } = prev;
      return rest;
    });
  };

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
    extraRows,
    extraOperationId,
    setExtraOperationId,
    extraWorkerId,
    setExtraWorkerId,
    operations,
    members,
    lines,
    teams,
    save,
    query,
    grid,
    rows,
    changedRows,
    resetEdits,
    addExtraRow,
    removeExtraRow,
    handleSave,
    lineOptions,
    teamOptions,
    changeQuantity,
  };
}

export type OutputEntryState = ReturnType<typeof useOutputEntry>;
