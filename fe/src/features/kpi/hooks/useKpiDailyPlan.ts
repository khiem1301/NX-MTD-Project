import { useMemo, useState } from 'react';
import type { KpiCalendar, KpiCalendarDay, SaveKpiDailyPlanPayload } from '../types';

/** Bản nháp KPI theo ngày: tính lại ngay số đã đặt và BTP còn lại; BE kiểm lại khi lưu. */
export function useKpiDailyPlan(data: KpiCalendar) {
  // Chỉ giữ các ô bị sửa (ngày → số mới, 0 = bỏ KPI ngày đó); ô trở về đúng số đã lưu thì bỏ khỏi danh sách.
  const [edits, setEdits] = useState<Record<string, number>>({});
  const byDate = useMemo(() => new Map(data.days.map((d) => [d.workDate, d])), [data.days]);

  const qtyOf = (day: KpiCalendarDay): number => edits[day.workDate] ?? day.targetQty;
  const isChanged = (day: KpiCalendarDay): boolean => day.workDate in edits;
  const isLocked = (day: KpiCalendarDay): boolean => day.workDate < data.firstEditableDate;

  const setQty = (day: KpiCalendarDay, value: number | null) => {
    const qty = Math.max(0, Math.floor(value ?? 0));
    setEdits(({ [day.workDate]: _old, ...rest }) =>
      qty === day.targetQty ? rest : { ...rest, [day.workDate]: qty },
    );
  };

  const allocated = data.days.reduce((sum, d) => sum + qtyOf(d), 0);
  const savedAllocated = data.days.reduce((sum, d) => sum + d.targetQty, 0);
  const pending = Math.max(0, allocated - data.finishedQty);
  const savedPending = Math.max(0, savedAllocated - data.finishedQty);
  const overTotal = allocated > data.targetFinishedQty;
  // Đang giảm thì vẫn cho lưu dù BTP đã thiếu từ trước, khớp với BE.
  const overBtp = pending > data.btp.available && pending > savedPending;

  const afterDueDays = data.days.filter((d) => d.workDate > data.dueDate && qtyOf(d) > 0).length;

  const payload: SaveKpiDailyPlanPayload = {
    dailyTargets: data.days
      .filter((d) => qtyOf(d) > 0)
      .map((d) => ({ workDate: d.workDate, targetQty: qtyOf(d) })),
  };

  return {
    byDate,
    qtyOf,
    isChanged,
    isLocked,
    setQty,
    allocated,
    unallocated: data.targetFinishedQty - allocated,
    btpLeft: data.btp.available - pending,
    overTotal,
    overBtp,
    afterDueDays,
    dirty: Object.keys(edits).length > 0,
    reset: () => setEdits({}),
    payload,
  };
}
