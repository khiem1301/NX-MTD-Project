import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import type { SpecImportPart, SpecImportResult } from '../types';

const text = MESSAGES.orders.importDone;

/** Câu thông báo sau khi lưu thành công: chỉ nêu các mục vừa nhập (không liệt kê "0 thành phần…" của bảng không nhập). */
const join = (parts: string[], removed: number, newMaterials: number) =>
  [
    parts.join(', '),
    removed > 0 ? fill(text.removed, removed) : '',
    newMaterials > 0 ? fill(text.newMaterials, newMaterials) : '',
  ]
    .filter(Boolean)
    .join('. ');

/** Định mức mã hàng / bìa của đơn. parts = các bảng vừa gửi nhập. */
export const specImportDoneMessage = (
  result: SpecImportResult,
  parts: readonly SpecImportPart[],
  newMaterials = 0,
): string => {
  const items: [SpecImportPart, string, number][] = [
    ['components', text.components, result.components],
    ['norms', text.norms, result.norms],
    ['groups', text.groups, result.markerGroups],
    ['markers', text.markers, result.orderMarkers],
  ];
  const done = items.filter(([part]) => parts.includes(part)).map(([, label, n]) => fill(label, n));
  const body = join(done, result.removed, newMaterials);
  const changed =
    items.some(([part, , n]) => parts.includes(part) && n > 0) ||
    result.removed > 0 ||
    newMaterials > 0;
  return changed ? `${text.savedPrefix} ${body}.` : text.nothingChanged;
};

export type OrderMaterialImportResult = { created: number; updated: number; removed?: number };

/** NVL của đơn. */
export const orderMaterialImportDoneMessage = (
  result: OrderMaterialImportResult | null | undefined,
  newMaterials = 0,
): string => {
  const created = result?.created ?? 0;
  const updated = result?.updated ?? 0;
  const removed = result?.removed ?? 0;
  if (created + updated + removed + newMaterials === 0) return text.nothingChanged;
  const body = join(
    [fill(text.materialsAdded, created), fill(text.materialsUpdated, updated)],
    removed,
    newMaterials,
  );
  return `${text.savedMaterialsPrefix} ${body}.`;
};
