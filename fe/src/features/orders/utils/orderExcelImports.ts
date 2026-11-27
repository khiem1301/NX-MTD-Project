import { MESSAGES } from '@/constants/messages';
import { isApiError } from '@/utils/apiError';
import { fill } from '@/utils/format';
import { excelBundleApi } from '../api/orderApi';
import type { SaveMaterialPayload } from '@/types/material';
import type {
  ExcelBundle,
  MaterialExcelRow,
  OrderMaterialExcelRow,
  SpecOrderMarkerExcelRow,
  SpecWorkbook,
} from '../types';
import {
  EMPTY_BUNDLE,
  EMPTY_WORKBOOK,
  createMaterials,
  refreshAfterImport,
  rowErrorsOf,
  type ProductImportOutcome,
  type ProductImportSelection,
} from './productExcelImports';
import type { ReviewTable } from './excelReview';

const text = MESSAGES.orders.orderExcel;
const productText = MESSAGES.orders.productExcel;

/** DEV-ALL-150: bảng theo đơn nhập ở form tạo / sửa đơn. */
export type OrderImportKind = 'markers' | 'orderMaterials';

/** Tab popup kiểm tra tương ứng từng mục form đơn (DEV-ALL-154). NVL của đơn đi kèm sheet NVL (DEV-ALL-192). */
export const ORDER_IMPORT_TABLE: Record<OrderImportKind, readonly ReviewTable[]> = {
  markers: ['markers'],
  orderMaterials: ['materials', 'orderMaterials'],
};

/** Dữ liệu đã đọc từ file, giữ ở form đến khi bấm Lưu đơn (không ghi gì trước đó). */
export type OrderImportSelection = {
  markers?: { fileName: string; rows: SpecOrderMarkerExcelRow[]; sizeColumns: string[] };
  orderMaterials?: {
    fileName: string;
    rows: OrderMaterialExcelRow[];
    /** DEV-ALL-192: sheet NVL (thông tin đầy đủ của NVL) và NVL mới sẽ thêm vào danh mục khi Lưu đơn. */
    materials: MaterialExcelRow[];
    newMaterials: SaveMaterialPayload[];
  };
};

/** Mã hàng của đơn sau khi lưu (Id + mã) để gắn bìa theo cột Mã hàng. */
export type OrderImportProduct = { productId: number; productCode: string };

/** DEV-ALL-193: NVL mới đã khai ở NVL của đơn, dạng dòng sheet NVL để máy chủ so khi kiểm file của mã hàng. */
export const pendingMaterialsOf = (selection: OrderImportSelection): MaterialExcelRow[] =>
  (selection.orderMaterials?.newMaterials ?? []).map((m, index) => ({
    rowNumber: index + 1,
    parseError: null,
    materialCode: m.materialCode ?? '',
    materialName: m.materialName,
    materialType: m.materialType,
    unit: m.unit,
    color: m.color ?? null,
    widthM: m.widthM ?? null,
  }));

/** Thêm NVL mới của NVL của đơn vào danh mục: phải chạy trước mọi lần nhập khác vì định mức mã hàng mới có thể dùng các NVL này. */
export const createOrderNewMaterials = async (
  selection: OrderImportSelection,
): Promise<ProductImportOutcome[]> => {
  const materials = selection.orderMaterials?.newMaterials ?? [];
  return materials.length > 0 ? [await createMaterials(materials)] : [];
};

/** Bỏ khỏi file của mã hàng các NVL mới đã khai ở NVL của đơn (đã được tạo một lần). */
export const withoutOrderNewMaterials = (
  product: ProductImportSelection,
  order: OrderImportSelection,
): ProductImportSelection => {
  const codes = new Set(pendingMaterialsOf(order).map((m) => m.materialCode.toUpperCase()));
  if (codes.size === 0 || !product.newMaterials) return product;
  return {
    ...product,
    newMaterials: product.newMaterials.filter(
      (m) => !codes.has((m.materialCode ?? '').toUpperCase()),
    ),
  };
};

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
    : {
        ...current,
        orderMaterials: {
          fileName,
          rows: bundle.orderMaterials,
          materials: bundle.materials,
          newMaterials: [],
        },
      };
};

export const orderRowCount = (s: OrderImportSelection, kind: OrderImportKind) =>
  s[kind]?.rows.length ?? 0;

export const hasOrderImports = (s: OrderImportSelection) =>
  orderRowCount(s, 'markers') > 0 || orderRowCount(s, 'orderMaterials') > 0;

/** Gói ExcelBundle từ dữ liệu đã xác nhận của một mục (mở lại popup kiểm tra / xem sửa). */
export const bundleOfOrderKind = (
  selection: OrderImportSelection,
  kind: OrderImportKind,
): ExcelBundle | null => {
  if (kind === 'markers') {
    const markers = selection.markers;
    if (!markers?.rows.length) return null;
    return {
      ...EMPTY_BUNDLE,
      spec: {
        ...EMPTY_WORKBOOK,
        hasOrderMarkers: true,
        orderMarkers: markers.rows,
        sizeColumns: markers.sizeColumns,
      },
    };
  }
  const materials = selection.orderMaterials;
  if (!materials?.rows.length) return null;
  return {
    ...EMPTY_BUNDLE,
    hasOrderMaterials: true,
    orderMaterials: materials.rows,
    hasMaterials: materials.materials.length > 0,
    materials: materials.materials,
  };
};

/** Lấy dữ liệu đã sửa trên popup vào selection của form đơn. */
export const takeOrderKind = (
  current: OrderImportSelection,
  kind: OrderImportKind,
  fileName: string,
  bundle: ExcelBundle,
  newMaterials: SaveMaterialPayload[] = [],
): OrderImportSelection => {
  if (kind === 'markers')
    return {
      ...current,
      markers: {
        fileName,
        rows: bundle.spec.orderMarkers,
        sizeColumns: bundle.spec.sizeColumns,
      },
    };
  return {
    ...current,
    orderMaterials: {
      fileName,
      rows: bundle.orderMaterials,
      materials: bundle.materials,
      newMaterials,
    },
  };
};

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
