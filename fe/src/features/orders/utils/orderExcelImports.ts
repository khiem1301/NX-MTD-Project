import { MESSAGES } from '@/constants/messages';
import { isApiError } from '@/utils/apiError';
import { fill } from '@/utils/format';
import { excelBundleApi } from '../api/orderApi';
import type {
  ExcelBundle,
  OrderMaterialExcelRow,
  SpecOrderMarkerExcelRow,
  SpecWorkbook,
} from '../types';
import {
  EMPTY_WORKBOOK,
  refreshAfterImport,
  rowErrorsOf,
  type ProductImportOutcome,
} from './productExcelImports';

const text = MESSAGES.orders.orderExcel;
const productText = MESSAGES.orders.productExcel;

/** DEV-ALL-150: bảng theo đơn nhập ở form tạo / sửa đơn. */
export type OrderImportKind = 'markers' | 'orderMaterials';

/** Dữ liệu đã đọc từ file, giữ ở form đến khi bấm Lưu đơn (không ghi gì trước đó). */
export type OrderImportSelection = {
  markers?: { fileName: string; rows: SpecOrderMarkerExcelRow[]; sizeColumns: string[] };
  orderMaterials?: { fileName: string; rows: OrderMaterialExcelRow[] };
};

/** Mã hàng của đơn sau khi lưu (Id + mã) để gắn bìa theo cột Mã hàng. */
export type OrderImportProduct = { productId: number; productCode: string };

/** Lấy đúng bảng của mục từ file đã đọc; file không có bảng đó thì trả null. */
export const pickOrderData = (
  current: OrderImportSelection,
  bundle: ExcelBundle,
  kind: OrderImportKind,
  fileName: string,
): OrderImportSelection | null => {
  if (kind === 'markers')
    return bundle.spec.orderMarkers.length === 0
      ? null
      : {
          ...current,
          markers: {
            fileName,
            rows: bundle.spec.orderMarkers,
            sizeColumns: bundle.spec.sizeColumns,
          },
        };
  return bundle.orderMaterials.length === 0
    ? null
    : { ...current, orderMaterials: { fileName, rows: bundle.orderMaterials } };
};

export const orderRowCount = (s: OrderImportSelection, kind: OrderImportKind) =>
  s[kind]?.rows.length ?? 0;

export const hasOrderImports = (s: OrderImportSelection) =>
  orderRowCount(s, 'markers') > 0 || orderRowCount(s, 'orderMaterials') > 0;

/**
 * Chia dòng bìa theo mã hàng của đơn. Dòng không ghi mã hàng chỉ nhận khi đơn có đúng một mã hàng;
 * mã hàng không có trong đơn thì bỏ qua và báo lại.
 */
export const groupMarkersByProduct = (
  rows: SpecOrderMarkerExcelRow[],
  products: OrderImportProduct[],
): { groups: Map<number, SpecOrderMarkerExcelRow[]>; skipped: string[] } => {
  const groups = new Map<number, SpecOrderMarkerExcelRow[]>();
  const skipped: string[] = [];
  const byCode = new Map(products.map((p) => [p.productCode.toUpperCase(), p.productId]));
  const only = products.length === 1 ? products[0]?.productId : undefined;
  for (const row of rows) {
    const code = row.productCode?.trim().toUpperCase();
    const productId = code ? byCode.get(code) : only;
    if (productId === undefined) {
      skipped.push(
        code
          ? fill(text.noProduct, row.rowNumber, row.productCode ?? '')
          : fill(text.needProductCode, row.rowNumber),
      );
      continue;
    }
    groups.set(productId, [...(groups.get(productId) ?? []), row]);
  }
  return { groups, skipped };
};

const failure = (error: unknown) =>
  isApiError(error) ? error.message : MESSAGES.common.unknownError;

const importMarkersOf = async (
  orderId: number,
  product: OrderImportProduct,
  rows: SpecOrderMarkerExcelRow[],
  sizeColumns: string[],
): Promise<ProductImportOutcome> => {
  const workbook: SpecWorkbook = {
    ...EMPTY_WORKBOOK,
    hasOrderMarkers: true,
    orderMarkers: rows,
    sizeColumns,
  };
  const base = { kind: 'markers' as const, label: product.productCode };
  try {
    const preview = await excelBundleApi.previewSpec(product.productId, workbook, orderId, [
      'markers',
    ]);
    const rowErrors = [...rowErrorsOf(preview.orderMarkers), ...preview.notes];
    if (!preview.canImport)
      return {
        ...base,
        ok: false,
        message: preview.crossErrors[0] ?? productText.nothing,
        rowErrors,
      };
    const result = await excelBundleApi.importSpec(product.productId, workbook, orderId, [
      'markers',
    ]);
    return { ...base, ok: true, message: result.message ?? MESSAGES.common.saved, rowErrors };
  } catch (error) {
    return { ...base, ok: false, message: failure(error), rowErrors: [] };
  }
};

const importOrderMaterials = async (
  orderId: number,
  rows: OrderMaterialExcelRow[],
): Promise<ProductImportOutcome> => {
  const base = { kind: 'orderMaterials' as const };
  try {
    const preview = await excelBundleApi.previewOrderMaterials(orderId, rows);
    const rowErrors = rowErrorsOf(preview.rows);
    if (preview.changeCount === 0)
      return { ...base, ok: false, message: productText.nothing, rowErrors };
    const result = await excelBundleApi.importOrderMaterials(orderId, rows);
    return { ...base, ok: true, message: result.message ?? MESSAGES.common.saved, rowErrors };
  } catch (error) {
    return { ...base, ok: false, message: failure(error), rowErrors: [] };
  }
};

/** Nhập NVL của đơn rồi bìa của từng mã hàng vào đơn vừa lưu; mục lỗi không chặn mục sau. */
export async function runOrderImports(
  orderId: number,
  selection: OrderImportSelection,
  products: OrderImportProduct[],
): Promise<ProductImportOutcome[]> {
  const outcomes: ProductImportOutcome[] = [];
  if (selection.orderMaterials?.rows.length)
    outcomes.push(await importOrderMaterials(orderId, selection.orderMaterials.rows));
  const markers = selection.markers;
  if (markers?.rows.length) {
    const { groups, skipped } = groupMarkersByProduct(markers.rows, products);
    if (skipped.length > 0)
      outcomes.push({
        kind: 'markers',
        ok: false,
        message: skipped[0] ?? '',
        rowErrors: skipped.slice(1),
      });
    for (const product of products) {
      const rows = groups.get(product.productId);
      if (rows) outcomes.push(await importMarkersOf(orderId, product, rows, markers.sizeColumns));
    }
  }
  if (outcomes.some((o) => o.ok)) await refreshAfterImport();
  return outcomes;
}
