import { useEffect, useMemo, useState } from 'react';
import { useApiMutation } from '@/hooks/useApiMutation';
import type { SaveMaterialPayload } from '@/types/material';
import { excelBundleApi } from '../api/orderApi';
import type { ExcelBundle, ExcelBundleCheck } from '../types';
import {
  addRow,
  defaultMaterial,
  patchRow,
  removeRow,
  rowStateOf,
  rowsOf,
  statusMaps,
  withMissingFields,
  type ReviewTable,
  type RowOf,
  type RowState,
} from '../utils/excelReview';

/** Chờ người dùng ngừng gõ rồi mới kiểm lại (ms). */
const RECHECK_DELAY_MS = 500;

/**
 * DEV-ALL-122/123: trạng thái popup kiểm tra Excel — bản nháp sửa được, tự kiểm lại sau mỗi lần sửa (chờ ngừng gõ), thông tin
 * NVL mới (chưa có trong danh mục) admin điền để thêm vào danh mục khi lưu. Không ghi gì.
 */
export function useExcelReview(
  initial: ExcelBundle,
  productId: number | null,
  tables: readonly ReviewTable[],
  orderId: number | null = null,
  replace = false,
) {
  const [draft, setDraft] = useState(initial);
  const check = useApiMutation(
    (bundle: ExcelBundle) => excelBundleApi.check({ productId, orderId, bundle, replace }),
    { invalidate: [] },
  );
  const { mutate } = check;

  useEffect(() => {
    const timer = setTimeout(() => mutate(draft), RECHECK_DELAY_MS);
    return () => clearTimeout(timer);
  }, [draft, mutate, replace]);

  // Giữ kết quả kiểm gần nhất trong lúc chờ lần kiểm mới: mutation xóa data khi gọi lại, làm khung NVL mới và trạng thái
  // dòng biến mất rồi hiện lại (nhấp nháy như tải lại trang, mất con trỏ) sau mỗi lần gõ.
  const [data, setData] = useState<ExcelBundleCheck>();
  if (check.data && check.data !== data) setData(check.data);

  const status = useMemo(
    () => withMissingFields(statusMaps(data), draft, tables),
    [data, draft, tables],
  );
  const stateOf = (table: ReviewTable, r: { rowNumber: number; parseError?: string | null }) =>
    rowStateOf(status[table].get(r.rowNumber), r.parseError);
  const counts = tables.reduce(
    (acc, t) => {
      for (const r of rowsOf(draft, t)) acc[stateOf(t, r)]++;
      return acc;
    },
    { valid: 0, error: 0, pending: 0, newMaterial: 0 } satisfies Record<RowState, number>,
  );

  // Mặc định theo gợi ý máy chủ, đè bởi phần admin đã sửa (theo mã NVL).
  const [materialEdits, setMaterialEdits] = useState<Record<string, Partial<SaveMaterialPayload>>>(
    {},
  );
  const newMaterials: SaveMaterialPayload[] = (data?.missingMaterials ?? []).map((m) => ({
    ...defaultMaterial(m),
    ...materialEdits[m.materialCode],
  }));

  return {
    draft,
    check,
    /** Kết quả kiểm gần nhất (không mất khi đang kiểm lại). */
    data,
    status,
    stateOf,
    counts,
    newMaterials,
    updateRow: <T extends ReviewTable>(table: T, rowNumber: number, patch: Partial<RowOf<T>>) =>
      setDraft((d) => patchRow(d, table, rowNumber, patch)),
    removeRow: (table: ReviewTable, rowNumber: number) =>
      setDraft((d) => removeRow(d, table, rowNumber)),
    addRow: (table: ReviewTable) => setDraft((d) => addRow(d, table)),
    recheck: () => mutate(draft),
    updateMaterial: (code: string, patch: Partial<SaveMaterialPayload>) =>
      setMaterialEdits((e) => ({ ...e, [code]: { ...e[code], ...patch } })),
  };
}
