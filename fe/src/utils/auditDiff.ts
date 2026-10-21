import { formatDate } from '@/utils/format';

type Json = Record<string, unknown>;
type SnapshotLine = { orderLineId: number; color: string; sizeCode: string; orderedQty: number; cmtUnitPrice: number };

const FIELD_LABEL: Record<string, string> = {
  OrderCode: 'Mã đơn',
  CustomerId: 'Khách hàng',
  ReceivedDate: 'Ngày nhận',
  DueDate: 'Ngày giao',
  Note: 'Ghi chú',
  Status: 'Trạng thái',
  StopReason: 'Lý do dừng',
  ProductCode: 'Mã hàng',
  ProductName: 'Tên',
  SampleImageUrl: 'Ảnh mẫu',
  Description: 'Mô tả',
};

const parse = (text: string | null): Json | null => {
  if (!text) return null;
  try {
    const value: unknown = JSON.parse(text);
    return typeof value === 'object' && value !== null ? (value as Json) : null;
  } catch {
    return null;
  }
};

const show = (key: string, value: unknown): string => {
  if (value === null || value === undefined || value === '') return '—';
  if (key === 'ReceivedDate' || key === 'DueDate') return formatDate(String(value));
  return String(value);
};

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
    if (prev.orderedQty !== l.orderedQty) result.push(`${lineKey(l)}: SL ${prev.orderedQty} → ${l.orderedQty}`);
    if (prev.cmtUnitPrice !== l.cmtUnitPrice) result.push(`${lineKey(l)}: đơn giá ${prev.cmtUnitPrice} → ${l.cmtUnitPrice}`);
  }
  for (const l of before) if (!seen.has(l.orderLineId) && !after.some((a) => a.orderLineId === l.orderLineId)) result.push(`Xóa dòng ${lineKey(l)}`);
  return result;
};

/** Các thay đổi đọc được của một bản ghi UPDATE: trường đổi (cũ → mới) và dòng màu × size thêm / xóa / đổi. */
export const describeChanges = (oldValues: string | null, newValues: string | null): string[] => {
  const before = parse(oldValues);
  const after = parse(newValues);
  if (!before || !after) return [];

  const result: string[] = [];
  for (const [key, value] of Object.entries(after)) {
    if (key === 'Lines' || !(key in before) || before[key] === value) continue;
    result.push(`${FIELD_LABEL[key] ?? key}: ${show(key, before[key])} → ${show(key, value)}`);
  }
  if (Array.isArray(before.Lines) && Array.isArray(after.Lines)) {
    result.push(...diffLines(before.Lines as SnapshotLine[], after.Lines as SnapshotLine[]));
  }
  return result;
};
