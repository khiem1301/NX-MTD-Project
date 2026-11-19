import { useState } from 'react';
import { useApiMutation } from '@/hooks/useApiMutation';
import { excelBundleApi } from '../api/orderApi';
import type { OrderImportRow, SaveProductPayload } from '../types';
import type { ProductImportSelection } from '../utils/productExcelImports';

/** Form Thêm mới mã hàng đang mở từ hộp nhập dòng hàng: giá trị điền sẵn và dữ liệu Excel đã đọc. */
export type NewProductFromImport = {
  code: string;
  values: Partial<SaveProductPayload>;
  imports: ProductImportSelection;
};

const same = (a: string, b: string) => a.trim().toUpperCase() === b.trim().toUpperCase();

/** Mã hàng trong file chưa có trong hệ thống: máy chủ không trả tên mã hàng cho dòng đó. */
export const unknownCodesOf = (rows: OrderImportRow[]) => [
  ...new Set(rows.filter((r) => r.productName === null && r.productCode).map((r) => r.productCode)),
];

/**
 * DEV-ALL-121/122: tạo mã hàng chưa có từ hộp "Nhập dòng hàng". Đọc mọi bảng của file (không ghi gì), lấy tên, mô tả của mã từ
 * sheet Mã hàng rồi mở form Thêm mới kèm dữ liệu đã đọc; form mở luôn popup kiểm tra. Chỉ tạo khi bấm Lưu ở form.
 */
export function useCreateProductFromImport(file: File | null) {
  const [creating, setCreating] = useState<NewProductFromImport>();
  const parse = useApiMutation((f: File) => excelBundleApi.parse(f), { invalidate: [] });

  const start = (code: string) => {
    if (!file) return;
    parse.mutate(file, {
      onSuccess: (parsed) => {
        const row = parsed.products.find((r) => same(r.productCode, code));
        const bundle = { ...parsed, hasProducts: row !== undefined, products: row ? [row] : [] };
        setCreating({
          code,
          values: {
            productCode: code,
            productName: row?.productName,
            description: row?.description ?? undefined,
          },
          imports: { bundle, fileName: file.name },
        });
      },
    });
  };

  return { creating, start, starting: parse.isPending, stop: () => setCreating(undefined) };
}
