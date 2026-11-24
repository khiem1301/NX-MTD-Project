import { useEffect, useState } from 'react';
import { useApiMutation } from '@/hooks/useApiMutation';
import { orderImportApi } from '../api/orderApi';
import type { OrderImportPreview, OrderImportRow } from '../types';
import { sortSizes } from '../utils/orderGrid';

/** Chờ người dùng ngừng gõ rồi mới kiểm lại (ms). */
const RECHECK_DELAY_MS = 500;

/**
 * DEV-ALL-178: bản nháp sửa được của hộp nhập dòng hàng. Bắt đầu từ kết quả xem trước của file; sửa ô / thêm / xóa dòng thì
 * máy chủ kiểm lại (mã hàng, màu, đơn giá, số lượng, trùng dòng). Kết quả kiểm gần nhất dùng để Áp dụng vào form. Không ghi gì.
 */
export function useOrderImportDraft(initial: OrderImportPreview) {
  const [rows, setRows] = useState<OrderImportRow[]>(initial.rows);
  const [sizes, setSizes] = useState<string[]>(initial.sizeColumns);
  const [result, setResult] = useState<OrderImportPreview>(initial);
  const check = useApiMutation(
    (body: { rows: OrderImportRow[]; sizes: string[] }) =>
      orderImportApi.previewData({
        sizeColumns: body.sizes,
        rows: body.rows.map((r) => ({
          rowNumber: r.rowNumber,
          productCode: r.productCode,
          color: r.color,
          cmtUnitPrice: r.cmtUnitPrice,
          quantities: r.quantities,
          parseError: null,
        })),
      }),
    { invalidate: [] },
  );
  const { mutate } = check;
  // Lần kiểm đầu trùng với kết quả xem trước nên bỏ qua; mỗi lần sửa sau đó thì kiểm lại.
  const [edited, setEdited] = useState(false);

  useEffect(() => {
    if (!edited) return;
    const timer = setTimeout(
      () => mutate({ rows, sizes }, { onSuccess: (data) => setResult(data) }),
      RECHECK_DELAY_MS,
    );
    return () => clearTimeout(timer);
  }, [rows, sizes, edited, mutate]);

  const change = (next: OrderImportRow[]) => {
    setEdited(true);
    setRows(next);
  };

  return {
    rows,
    sizes,
    result,
    checking: edited && (check.isPending || rowsDiffer(rows, result.rows)),
    checkError: check.error?.message,
    update: (rowNumber: number, patch: Partial<OrderImportRow>) =>
      change(rows.map((r) => (r.rowNumber === rowNumber ? { ...r, ...patch } : r))),
    setQty: (rowNumber: number, sizeCode: string, qty: number | null) =>
      change(
        rows.map((r) => {
          if (r.rowNumber !== rowNumber) return r;
          const rest = r.quantities.filter((q) => q.sizeCode !== sizeCode);
          return { ...r, quantities: qty && qty > 0 ? [...rest, { sizeCode, qty }] : rest };
        }),
      ),
    remove: (rowNumber: number) => change(rows.filter((r) => r.rowNumber !== rowNumber)),
    add: () => {
      const next = Math.max(0, ...rows.map((r) => r.rowNumber)) + 1;
      change([
        ...rows,
        {
          rowNumber: next,
          productCode: '',
          productId: null,
          color: '',
          cmtUnitPrice: null,
          quantities: [],
          error: null,
        },
      ]);
    },
    addSize: (size: string) => {
      const code = size.trim().toUpperCase();
      if (!code || sizes.includes(code)) return;
      setEdited(true);
      setSizes(sortSizes([...sizes, code]));
    },
  };
}

/** Bản nháp đã khác kết quả kiểm gần nhất (còn chờ kiểm): chưa cho Áp dụng. */
const rowsDiffer = (draft: OrderImportRow[], checked: OrderImportRow[]) =>
  draft.length !== checked.length ||
  draft.some((d) => {
    const c = checked.find((x) => x.rowNumber === d.rowNumber);
    return (
      !c ||
      c.productCode.toUpperCase() !== d.productCode.trim().toUpperCase() ||
      c.color !== d.color.trim() ||
      c.cmtUnitPrice !== d.cmtUnitPrice ||
      JSON.stringify([...c.quantities].sort(bySize)) !==
        JSON.stringify([...d.quantities].sort(bySize))
    );
  });

const bySize = (a: { sizeCode: string }, b: { sizeCode: string }) =>
  a.sizeCode.localeCompare(b.sizeCode);
