import { useState } from 'react';
import { message, type FormInstance } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import type { ProductExcelRow, ProductListItem, SaveProductPayload } from '../types';
import {
  hasImportFiles,
  missingKinds,
  requiredKindsFor,
  runProductImports,
  showProductImportResults,
  type ProductImportSelection,
} from '../utils/productExcelImports';
import { excelBundleApi } from '../api/orderApi';
import { bundleErrorCount, incompleteMaterials } from '../utils/excelReview';

const text = MESSAGES.orders.productExcel;
const NO_IMPORTS: ProductImportSelection = {};

type Options = {
  form: FormInstance<SaveProductPayload>;
  product: ProductListItem | null;
  allowImports: boolean;
  onDeferImports?: (productId: number, code: string, selection: ProductImportSelection) => void;
  initialImports?: ProductImportSelection;
  onClose: () => void;
};

/**
 * DEV-ALL-121..124: phần nhập Excel của form mã hàng — giữ dữ liệu đã kiểm, kiểm mục bắt buộc khi Thêm mới, sau khi lưu mã hàng
 * thì nhập ngay (màn Mã hàng) hoặc giao cho form đơn nhập sau Lưu đơn, điền Mã / Tên / Mô tả từ sheet Mã hàng.
 */
export function useProductFormImports({
  form,
  product,
  allowImports,
  onDeferImports,
  initialImports,
  onClose,
}: Options) {
  const [imports, setImports] = useState<ProductImportSelection>(() =>
    initialImports && product === null ? initialImports : NO_IMPORTS,
  );
  const [importing, setImporting] = useState(false);
  const [checking, setChecking] = useState(false);
  const [showMissing, setShowMissing] = useState(false);
  const required = allowImports ? requiredKindsFor(product === null) : [];

  // Bỏ dữ liệu đã chọn mỗi lần đóng để lần mở sau bắt đầu trống.
  const close = () => {
    setImports(NO_IMPORTS);
    setShowMissing(false);
    onClose();
  };

  /** Thêm mới thiếu mục bắt buộc thì báo, tô đỏ ô thiếu và trả false (không lưu). */
  const checkRequired = () => {
    const missing = missingKinds(imports, required);
    if (missing.length === 0) return true;
    setShowMissing(true);
    void message.error(fill(text.requiredMissing, missing.map((k) => text.kinds[k]).join(', ')));
    return false;
  };

  /**
   * Thêm mới: kiểm lại file Excel đã chọn với máy chủ ngay trước khi lưu. Còn dòng lỗi, thiếu trường, lỗi chéo hoặc NVL mới
   * thiếu thông tin thì báo, trả false (không lưu, form vẫn mở để bấm "Xem / sửa lại").
   */
  const checkNoErrors = async () => {
    const bundle = imports.bundle;
    if (product !== null || !allowImports || !bundle) return true;
    setChecking(true);
    try {
      const result = await excelBundleApi.check({ productId: null, orderId: null, bundle });
      const { rows, cross } = bundleErrorCount(bundle, result);
      const incomplete = incompleteMaterials(imports.newMaterials ?? []);
      if (rows === 0 && cross === 0 && incomplete.length === 0) return true;
      setShowMissing(true);
      void message.error(fill(text.saveBlockedByErrors, rows + cross + incomplete.length));
      return false;
    } catch {
      void message.error(text.saveCheckFailed);
      return false;
    } finally {
      setChecking(false);
    }
  };

  /** Mã hàng đã lưu: nhập ngay (màn Mã hàng), giao cho form đơn (nhập sau Lưu đơn) hoặc đóng nếu không có gì để nhập. */
  const afterSave = (productId: number, code: string) => {
    if (!allowImports || !hasImportFiles(imports)) return close();
    if (onDeferImports) {
      onDeferImports(productId, code, imports);
      return close();
    }
    setImporting(true);
    void runProductImports(productId, imports)
      .then((outcomes) => {
        showProductImportResults(code, outcomes);
        close();
      })
      .finally(() => setImporting(false));
  };

  /** Thêm mới: điền Mã, Mô tả; Sửa: chỉ điền Mô tả khi mã trong file trùng mã đang sửa. */
  const fillFromRow = (row: ProductExcelRow) => {
    const code = row.productCode.trim().toUpperCase();
    const values = { description: row.description ?? undefined };
    if (product) {
      if (code !== product.productCode.toUpperCase()) {
        void message.warning(fill(text.otherProductInFile, row.productCode));
        return;
      }
      form.setFieldsValue(values);
      void message.success(text.filledFromExcelEdit);
      return;
    }
    form.setFieldsValue({ productCode: code, ...values });
    void message.success(fill(text.filledFromExcel, row.productCode));
  };

  return {
    imports,
    setImports,
    importing: importing || checking,
    checkNoErrors,
    required,
    showMissing,
    close,
    checkRequired,
    afterSave,
    fillFromRow,
  };
}
