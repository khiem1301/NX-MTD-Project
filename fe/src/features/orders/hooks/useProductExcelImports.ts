import { useState } from 'react';
import { useApiMutation } from '@/hooks/useApiMutation';
import type { SaveMaterialPayload } from '@/types/material';
import { excelBundleApi } from '../api/orderApi';
import type { ExcelBundle, ProductExcelRow } from '../types';
import {
  EMPTY_BUNDLE,
  takeKind,
  withoutOrderData,
  type ProductImportKind,
  type ProductImportSelection,
} from '../utils/productExcelImports';

/** Popup đang mở: kind = chọn file cho một mục (chỉ lấy bảng của mục đó), không có = file chung / xem lại. */
export type Reviewing = { bundle: ExcelBundle; fileName: string; kind?: ProductImportKind };

type Options = {
  value: ProductImportSelection;
  onChange: (next: ProductImportSelection) => void;
  onProductRow?: (row: ProductExcelRow) => void;
  /** Mở popup kiểm tra ngay khi gắn (dữ liệu đã đọc sẵn). */
  autoReview: boolean;
};

/**
 * DEV-ALL-121/122/123: khung "Nhập từ Excel" của form mã hàng — đọc file (không ghi gì) rồi mở popup kiểm tra; Xác nhận thì giữ
 * bản đã sửa (file chung thay cả bộ, file của một mục chỉ thay bảng đó) và danh sách NVL mới chọn thêm vào danh mục.
 */
export function useProductExcelImports({ value, onChange, onProductRow, autoReview }: Options) {
  const [reviewing, setReviewing] = useState<Reviewing | null>(() =>
    autoReview && value.bundle ? { bundle: value.bundle, fileName: value.fileName ?? '' } : null,
  );
  const parse = useApiMutation((file: File) => excelBundleApi.parse(file), { invalidate: [] });
  const [parsingKind, setParsingKind] = useState<ProductImportKind | 'all'>();

  const openFile = (file: File, kind?: ProductImportKind) => {
    setParsingKind(kind ?? 'all');
    parse.mutate(file, {
      onSuccess: (bundle) =>
        setReviewing({ bundle: withoutOrderData(bundle), fileName: file.name, kind }),
      onSettled: () => setParsingKind(undefined),
    });
  };

  const confirm = (edited: ExcelBundle, materials: SaveMaterialPayload[]) => {
    if (!reviewing) return;
    const { kind, fileName } = reviewing;
    const bundle = kind ? takeKind(value.bundle ?? EMPTY_BUNDLE, edited, kind) : edited;
    // File chung thay cả danh sách NVL mới; file của một mục thì gộp theo mã.
    const newMaterials = kind
      ? [
          ...(value.newMaterials ?? []).filter(
            (m) => !materials.some((x) => x.materialName === m.materialName),
          ),
          ...materials,
        ]
      : materials;
    onChange({
      ...value,
      bundle,
      newMaterials,
      fileName: kind ? (value.fileName ?? fileName) : fileName,
    });
    const row = edited.products[0];
    if (!kind && row) onProductRow?.(row);
    setReviewing(null);
  };

  return {
    reviewing,
    openFile,
    confirm,
    review: () =>
      value.bundle && setReviewing({ bundle: value.bundle, fileName: value.fileName ?? '' }),
    closeReview: () => setReviewing(null),
    parsing: (kind: ProductImportKind | 'all') => parse.isPending && parsingKind === kind,
  };
}
