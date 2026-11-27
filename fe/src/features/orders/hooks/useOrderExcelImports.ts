import { message } from 'antd';
import { useState } from 'react';
import { useApiMutation } from '@/hooks/useApiMutation';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import type { SaveMaterialPayload } from '@/types/material';
import { excelBundleApi } from '../api/orderApi';
import type { ExcelBundle } from '../types';
import {
  ORDER_IMPORT_TABLE,
  bundleOfOrderKind,
  pickOrderData,
  takeOrderKind,
  type OrderImportKind,
  type OrderImportSelection,
} from '../utils/orderExcelImports';
import type { ReviewTable } from '../utils/excelReview';

const text = MESSAGES.orders.orderExcel;

/** Popup đang mở: kind = chọn file / xem lại một mục Bìa hoặc NVL của đơn. */
export type OrderReviewing = {
  bundle: ExcelBundle;
  fileName: string;
  kind: OrderImportKind;
};

/**
 * DEV-ALL-150/154: đọc file Excel Bìa của đơn / NVL của đơn ở form đơn → mở popup kiểm tra sửa trực tiếp
 * (như form mã hàng); Xác nhận thì giữ ở selection đến khi Lưu đơn.
 */
export function useOrderExcelImports(onChange: () => void) {
  const [selection, setSelection] = useState<OrderImportSelection>({});
  const [reading, setReading] = useState<OrderImportKind>();
  const [reviewing, setReviewing] = useState<OrderReviewing | null>(null);
  const parse = useApiMutation((file: File) => excelBundleApi.parse(file), { invalidate: [] });

  const choose = (kind: OrderImportKind, file: File) => {
    setReading(kind);
    parse.mutate(file, {
      onSuccess: (bundle) => {
        const next = pickOrderData(selection, bundle, kind, file.name);
        if (!next) {
          void message.warning(fill(text.noTable, MESSAGES.orders.productExcel.kinds[kind]));
          return;
        }
        const reviewBundle = bundleOfOrderKind(next, kind);
        if (!reviewBundle) {
          void message.warning(fill(text.noTable, MESSAGES.orders.productExcel.kinds[kind]));
          return;
        }
        setReviewing({ bundle: reviewBundle, fileName: file.name, kind });
      },
      onSettled: () => setReading(undefined),
    });
  };

  const confirm = (edited: ExcelBundle, newMaterials: SaveMaterialPayload[], _replace: boolean) => {
    if (!reviewing) return;
    const { kind, fileName } = reviewing;
    setSelection((s) => takeOrderKind(s, kind, fileName, edited, newMaterials));
    setReviewing(null);
    onChange();
  };

  const clear = (kind: OrderImportKind) => {
    setSelection((s) => ({ ...s, [kind]: undefined }));
    onChange();
  };

  const reviewAgain = (kind: OrderImportKind) => {
    const bundle = bundleOfOrderKind(selection, kind);
    const fileName = selection[kind]?.fileName;
    if (!bundle || !fileName) return;
    setReviewing({ bundle, fileName, kind });
  };

  const onlyTables = (kind: OrderImportKind): readonly ReviewTable[] => ORDER_IMPORT_TABLE[kind];

  return {
    selection,
    reading,
    reviewing,
    choose,
    confirm,
    clear,
    reviewAgain,
    closeReview: () => setReviewing(null),
    onlyTables,
    reset: () => setSelection({}),
  };
}
