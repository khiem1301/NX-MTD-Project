import { message } from 'antd';
import { useState } from 'react';
import { useApiMutation } from '@/hooks/useApiMutation';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import { excelBundleApi } from '../api/orderApi';
import {
  pickOrderData,
  type OrderImportKind,
  type OrderImportSelection,
} from '../utils/orderExcelImports';

const text = MESSAGES.orders.orderExcel;

/**
 * DEV-ALL-150: đọc file Excel Bìa của đơn / NVL của đơn ở form đơn. Chỉ đọc (không ghi); dữ liệu nằm trong selection
 * đến khi Lưu đơn.
 */
export function useOrderExcelImports(onChange: () => void) {
  const [selection, setSelection] = useState<OrderImportSelection>({});
  const [reading, setReading] = useState<OrderImportKind>();
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
        setSelection(next);
        onChange();
      },
      onSettled: () => setReading(undefined),
    });
  };

  const clear = (kind: OrderImportKind) => {
    setSelection((s) => ({ ...s, [kind]: undefined }));
    onChange();
  };

  return { selection, reading, choose, clear, reset: () => setSelection({}) };
}
