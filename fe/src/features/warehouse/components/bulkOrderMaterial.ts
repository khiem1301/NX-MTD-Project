import { MESSAGES } from '@/constants/messages';
import { LABELS } from '../constants';
import type { OrderMaterial } from '../types';
import type { MaterialOption } from '@/types/lookup';
import { noteProblem, qtyProblem } from '../utils/orderMaterialRules';

export type Draft = {
  requiredQty: number;
  customerQty: number;
  note: string;
};

/** Một dòng của bảng sửa cả đơn: NVL đã có của đơn (original) hoặc NVL vừa thêm (chưa có original). */
export type BulkLine = {
  key: string;
  original?: OrderMaterial;
  materialId: number | null;
  draft: Draft;
};

export const EMPTY_DRAFT: Draft = {
  requiredQty: 0,
  customerQty: 0,
  note: '',
};

export const toDraft = (om: OrderMaterial): Draft => ({
  requiredQty: om.requiredQty,
  customerQty: om.customerQty,
  note: om.note ?? '',
});

export const toLine = (om: OrderMaterial): BulkLine => ({
  key: `om-${om.orderMaterialId}`,
  original: om,
  materialId: om.materialId,
  draft: toDraft(om),
});

/** Lịch khách giao khai ở cả đơn (DEV-ALL-95) nên dòng NVL chỉ còn số lượng và ghi chú. */
export const applyDraftChange = (draft: Draft, change: Partial<Draft>): Draft => ({
  ...draft,
  ...change,
});

/** Dữ liệu gửi API của một dòng đã chọn NVL. */
export const toLinePayload = (line: BulkLine) => ({
  materialId: line.materialId as number,
  requiredQty: line.draft.requiredQty,
  customerQty: line.draft.customerQty,
  note: line.draft.note.trim() || undefined,
});

/** Dòng cần gửi lên: NVL mới thêm, hoặc NVL cũ có thay đổi so với bản gốc. */
export const isChanged = (line: BulkLine): boolean => {
  if (!line.original) return true;
  const o = toDraft(line.original);
  const d = line.draft;
  return o.requiredQty !== d.requiredQty || o.customerQty !== d.customerQty || o.note !== d.note;
};

/**
 * Lỗi của một dòng theo quy tắc form từng dòng: phải chọn NVL, từng số lượng hợp lệ (không âm, tối đa 100.000.000, 3 số lẻ),
 * có số cần hoặc khách cấp; ghi chú tối đa 500 ký tự.
 */
export const rowError = (line: BulkLine): string | null => {
  if (line.materialId === null) return MESSAGES.common.required;
  const d = line.draft;
  const problem =
    qtyProblem(LABELS.requiredQtyName, d.requiredQty) ??
    qtyProblem(LABELS.customerQtyName, d.customerQty) ??
    noteProblem(d.note);
  if (problem) return problem;
  return d.requiredQty <= 0 && d.customerQty <= 0 ? LABELS.qtyRequiredOrCustomer : null;
};

/** Dòng mới chưa chọn NVL luôn hiện để không mất dòng đang nhập khi tìm kiếm. */
export const matchesKeyword = (
  line: BulkLine,
  keyword: string,
  options: MaterialOption[],
): boolean => {
  const term = keyword.trim().toLowerCase();
  if (!term || line.materialId === null) return true;
  const option = options.find((m) => m.materialId === line.materialId);
  return [
    line.original?.materialCode ?? option?.materialCode,
    line.original?.materialName ?? option?.materialName,
    line.original?.materialColor ?? option?.color,
  ].some((text) => text?.toLowerCase().includes(term));
};
