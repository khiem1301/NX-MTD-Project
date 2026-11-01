import { auditFieldLabel, formatAuditValue } from '@/utils/auditLabels';

type Json = Record<string, unknown>;
type SnapshotLine = {
  orderLineId: number;
  color: string;
  sizeCode: string;
  orderedQty: number;
  cmtUnitPrice: number;
};

/** Một dòng thay đổi đã dịch sang tiếng Việt: trường, giá trị trước (rỗng khi tạo mới) và giá trị sau. */
export type ChangeRow = { field: string; before: string | null; after: string };

const LINES_FIELD = 'Dòng đơn';

const parse = (text: string | null): Json | null => {
  if (!text) return null;
  try {
    const value: unknown = JSON.parse(text);
    return typeof value === 'object' && value !== null && !Array.isArray(value)
      ? (value as Json)
      : null;
  } catch {
    return null;
  }
};

const isOrderLines = (value: unknown): value is SnapshotLine[] =>
  Array.isArray(value) &&
  value.every(
    (l) => typeof l === 'object' && l !== null && 'orderLineId' in l && 'orderedQty' in l,
  );

const lineKey = (l: SnapshotLine): string => `${l.color} / ${l.sizeCode}`;

const diffLines = (before: SnapshotLine[], after: SnapshotLine[]): string[] => {
  const result: string[] = [];
  const old = new Map(before.map((l) => [l.orderLineId, l]));
  const seen = new Set<number>();
  for (const l of after) {
    const prev = old.get(l.orderLineId);
    if (!prev) {
      result.push(`Thêm dòng ${lineKey(l)}: ${l.orderedQty}`);
      continue;
    }
    seen.add(l.orderLineId);
    if (prev.orderedQty !== l.orderedQty)
      result.push(`${lineKey(l)}: SL ${prev.orderedQty} → ${l.orderedQty}`);
    if (prev.cmtUnitPrice !== l.cmtUnitPrice)
      result.push(`${lineKey(l)}: đơn giá ${prev.cmtUnitPrice} → ${l.cmtUnitPrice}`);
  }
  for (const l of before)
    if (!seen.has(l.orderLineId) && !after.some((a) => a.orderLineId === l.orderLineId))
      result.push(`Xóa dòng ${lineKey(l)}`);
  return result;
};

/**
 * Nội dung một bản ghi log dưới dạng bảng "trường – trước – sau" dễ đọc.
 * Tạo mới / hành động không có giá trị cũ: liệt kê giá trị mới (before = null). Cập nhật: chỉ các trường đổi.
 */
export const buildChangeRows = (
  oldValues: string | null,
  newValues: string | null,
): ChangeRow[] => {
  const before = parse(oldValues);
  const after = parse(newValues);
  if (!after && !before) return [];

  const rows: ChangeRow[] = [];
  for (const [key, value] of Object.entries(after ?? {})) {
    if (key === 'Lines' && isOrderLines(value)) continue;
    if (before && key in before) {
      if (JSON.stringify(before[key]) === JSON.stringify(value)) continue;
      rows.push({
        field: auditFieldLabel(key),
        before: formatAuditValue(key, before[key]),
        after: formatAuditValue(key, value),
      });
    } else {
      rows.push({
        field: auditFieldLabel(key),
        before: before ? '—' : null,
        after: formatAuditValue(key, value),
      });
    }
  }
  if (!after && before) {
    for (const [key, value] of Object.entries(before))
      rows.push({ field: auditFieldLabel(key), before: formatAuditValue(key, value), after: '—' });
  }
  if (before && after && isOrderLines(before.Lines) && isOrderLines(after.Lines)) {
    for (const note of diffLines(before.Lines, after.Lines))
      rows.push({ field: LINES_FIELD, before: null, after: note });
  }
  return rows;
};

/** Các thay đổi đọc được của một bản ghi UPDATE dạng từng câu (dùng ở lịch sử trong màn chi tiết). */
export const describeChanges = (oldValues: string | null, newValues: string | null): string[] => {
  if (!parse(oldValues) || !parse(newValues)) return [];
  return buildChangeRows(oldValues, newValues).map((r) => {
    if (r.field === LINES_FIELD) return r.after;
    return r.before === null ? `${r.field}: ${r.after}` : `${r.field}: ${r.before} → ${r.after}`;
  });
};
