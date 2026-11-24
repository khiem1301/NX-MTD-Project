import { MATERIAL_TYPE } from '@/constants/warehouse';
import type { SaveMaterialPayload } from '@/types/material';
import {
  EXCEL_COMPONENT_TYPE,
  EXCEL_MARKER_STATUS,
  EXCEL_ROW_ACTION,
  NEW_MATERIAL_DEFAULTS,
} from '../constants';
import type { ExcelBundle, ExcelBundleCheck, MissingMaterial } from '../types';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';

const col = MESSAGES.orders.excelReview.columns;

/** DEV-ALL-122: bảng sửa được trong popup kiểm tra Excel (products = sheet Mã hàng). */
export type ReviewTable =
  'products' | 'components' | 'norms' | 'groups' | 'markers' | 'orderMaterials';

export const REVIEW_TABLES: readonly ReviewTable[] = [
  'products',
  'components',
  'norms',
  'groups',
  'markers',
  'orderMaterials',
];

/** Kết quả kiểm một dòng (máy chủ trả). */
export type RowStatus = { action: string; error: string | null };

/** Phân loại dòng trên popup: lỗi (bỏ qua khi lưu), chờ kiểm khi Lưu đơn, dùng NVL chưa có, hợp lệ. */
export type RowState = 'error' | 'pending' | 'newMaterial' | 'valid';

/** Dòng của từng bảng. */
export type RowsOf = {
  products: ExcelBundle['products'];
  components: ExcelBundle['spec']['components'];
  norms: ExcelBundle['spec']['norms'];
  groups: ExcelBundle['spec']['markerGroups'];
  markers: ExcelBundle['spec']['orderMarkers'];
  orderMaterials: ExcelBundle['orderMaterials'];
};
export type RowOf<T extends ReviewTable> = RowsOf[T][number];

const GETTERS: { [K in ReviewTable]: (b: ExcelBundle) => RowsOf[K] } = {
  products: (b) => b.products,
  components: (b) => b.spec.components,
  norms: (b) => b.spec.norms,
  groups: (b) => b.spec.markerGroups,
  markers: (b) => b.spec.orderMarkers,
  orderMaterials: (b) => b.orderMaterials,
};

const SETTERS: { [K in ReviewTable]: (b: ExcelBundle, rows: RowsOf[K]) => ExcelBundle } = {
  products: (b, rows) => ({ ...b, products: rows }),
  components: (b, rows) => ({ ...b, spec: { ...b.spec, components: rows } }),
  norms: (b, rows) => ({ ...b, spec: { ...b.spec, norms: rows } }),
  groups: (b, rows) => ({ ...b, spec: { ...b.spec, markerGroups: rows } }),
  markers: (b, rows) => ({ ...b, spec: { ...b.spec, orderMarkers: rows } }),
  orderMaterials: (b, rows) => ({ ...b, orderMaterials: rows }),
};

/**
 * Chỉ giữ các bảng đang nhập (bảng khác coi như không có trong file: cờ Has… tắt, không dòng). Dùng khi gửi kiểm và nhập để mỗi nơi
 * nhập Excel chỉ đụng dữ liệu của mình (vd nhập Bìa cắt không kiểm, không ghi định mức / NVL của đơn dù file có các sheet đó).
 */
export const restrictBundle = (bundle: ExcelBundle, tables: readonly ReviewTable[]): ExcelBundle => {
  const keep = (t: ReviewTable) => tables.includes(t);
  return {
    hasProducts: bundle.hasProducts && keep('products'),
    products: keep('products') ? bundle.products : [],
    spec: {
      hasComponents: bundle.spec.hasComponents && keep('components'),
      components: keep('components') ? bundle.spec.components : [],
      hasNorms: bundle.spec.hasNorms && keep('norms'),
      norms: keep('norms') ? bundle.spec.norms : [],
      hasMarkerGroups: bundle.spec.hasMarkerGroups && keep('groups'),
      markerGroups: keep('groups') ? bundle.spec.markerGroups : [],
      hasOrderMarkers: bundle.spec.hasOrderMarkers && keep('markers'),
      orderMarkers: keep('markers') ? bundle.spec.orderMarkers : [],
      sizeColumns: bundle.spec.sizeColumns,
    },
    hasOrderMaterials: bundle.hasOrderMaterials && keep('orderMaterials'),
    orderMaterials: keep('orderMaterials') ? bundle.orderMaterials : [],
  };
};

/** Dòng trống khi bấm Thêm dòng. */
const BLANK: { [K in ReviewTable]: (rowNumber: number) => RowOf<K> } = {
  products: (rowNumber) => ({ rowNumber, productCode: '', description: null }),
  components: (rowNumber) => ({
    rowNumber,
    name: '',
    componentType: EXCEL_COMPONENT_TYPE.PANEL,
    piecesPerGarment: 1,
    parseError: null,
  }),
  norms: (rowNumber) => ({
    rowNumber,
    color: '',
    componentName: '',
    materialCode: '',
    specNote: null,
    parseError: null,
  }),
  groups: (rowNumber) => ({
    rowNumber,
    markerNo: 1,
    componentNames: [],
    colors: [],
    parseError: null,
  }),
  markers: (rowNumber) => ({
    rowNumber,
    productCode: null,
    markerNo: 1,
    markerCode: '',
    lengthM: null,
    widthM: null,
    status: EXCEL_MARKER_STATUS.ACTIVE,
    ratios: [],
    parseError: null,
  }),
  orderMaterials: (rowNumber) => ({
    rowNumber,
    materialCode: '',
    materialName: null,
    unit: null,
    requiredQty: null,
    customerQty: null,
    note: null,
    parseError: null,
  }),
};

export const rowsOf = <T extends ReviewTable>(bundle: ExcelBundle, table: T): RowsOf[T] =>
  GETTERS[table](bundle);

/**
 * Thay dòng của một bảng theo hàm biến đổi. TypeScript không thu hẹp được mảng theo bảng generic (RowsOf[T] là hợp các mảng),
 * nên ép kiểu đúng một chỗ ở đây; mọi dòng vào / ra đều đúng kiểu của bảng đó.
 */
const mapRows = <T extends ReviewTable>(
  bundle: ExcelBundle,
  table: T,
  fn: (rows: RowOf<T>[]) => RowOf<T>[],
): ExcelBundle => SETTERS[table](bundle, fn(rowsOf(bundle, table) as RowOf<T>[]) as RowsOf[T]);

/** Sửa một dòng: gộp giá trị mới, xóa lỗi định dạng lúc đọc file (máy chủ kiểm lại nội dung). */
export const patchRow = <T extends ReviewTable>(
  bundle: ExcelBundle,
  table: T,
  rowNumber: number,
  patch: Partial<RowOf<T>>,
): ExcelBundle =>
  mapRows(bundle, table, (rows) =>
    rows.map((r) =>
      r.rowNumber === rowNumber
        ? { ...r, ...patch, ...('parseError' in r && { parseError: null }) }
        : r,
    ),
  );

export const removeRow = (bundle: ExcelBundle, table: ReviewTable, rowNumber: number) =>
  mapRows(bundle, table, (rows) => rows.filter((r) => r.rowNumber !== rowNumber));

/** Thêm dòng trống; số dòng mới lớn hơn mọi số dòng đang có để không trùng kết quả kiểm. */
export const addRow = (bundle: ExcelBundle, table: ReviewTable) => {
  const used = REVIEW_TABLES.flatMap((t) => rowsOf(bundle, t).map((r) => r.rowNumber));
  const next = Math.max(0, ...used) + 1;
  return mapRows(bundle, table, (rows) => [...rows, BLANK[table](next)]);
};

/** Kết quả kiểm của từng dòng, theo bảng và rowNumber. */
export const statusMaps = (
  check: ExcelBundleCheck | undefined,
): Record<ReviewTable, Map<number, RowStatus>> => {
  const toMap = (
    rows: { rowNumber: number; action: string; error?: string | null; note?: string | null }[],
  ) =>
    new Map(rows.map((r) => [r.rowNumber, { action: r.action, error: r.error ?? r.note ?? null }]));
  return {
    products: toMap(check?.products?.rows ?? []),
    components: toMap(check?.spec.components ?? []),
    norms: toMap(check?.spec.norms ?? []),
    groups: toMap(check?.spec.markerGroups ?? []),
    markers: toMap(check?.spec.orderMarkers ?? []),
    orderMaterials: toMap(check?.orderMaterials?.rows ?? []),
  };
};

const blank = (v: unknown) => v === null || v === undefined || String(v).trim() === '';

/** Trường bắt buộc của từng bảng: [nhãn cột, có thiếu không]. */
const REQUIRED: { [K in ReviewTable]: (r: RowOf<K>) => [string, boolean][] } = {
  products: (r) => [[col.productCode, blank(r.productCode)]],
  components: (r) => [
    [col.name, blank(r.name)],
    [col.type, blank(r.componentType)],
    [col.pieces, blank(r.piecesPerGarment)],
  ],
  norms: (r) => [
    [col.color, blank(r.color)],
    [col.component, blank(r.componentName)],
    [col.material, blank(r.materialCode)],
  ],
  groups: (r) => [
    [col.markerNo, blank(r.markerNo)],
    [col.groupComponents, r.componentNames.length === 0],
    [col.groupColors, r.colors.length === 0],
  ],
  markers: (r) => [
    [col.productCode, blank(r.productCode)],
    [col.markerNo, blank(r.markerNo)],
    [col.markerCode, blank(r.markerCode)],
    [col.length, blank(r.lengthM)],
    [col.width, blank(r.widthM)],
    [col.markerStatus, blank(r.status)],
    [col.sizeRatios, r.ratios.length === 0],
  ],
  orderMaterials: (r) => [
    [col.material, blank(r.materialCode)],
    [col.requiredQty, blank(r.requiredQty)],
  ],
};

/** Lỗi thiếu trường bắt buộc của một dòng (kiểm ngay trên máy, không chờ máy chủ); null khi đủ. */
export const missingFieldsError = <T extends ReviewTable>(table: T, row: RowOf<T>) => {
  const missing = REQUIRED[table](row)
    .filter(([, m]) => m)
    .map(([label]) => label);
  return missing.length > 0
    ? fill(MESSAGES.orders.excelReview.missingFields, missing.join(', '))
    : null;
};

/** Gộp lỗi thiếu trường vào kết quả kiểm của máy chủ: dòng thiếu trường luôn là lỗi. */
export const withMissingFields = (
  maps: Record<ReviewTable, Map<number, RowStatus>>,
  bundle: ExcelBundle,
  tables: readonly ReviewTable[],
) => {
  const out = { ...maps };
  for (const t of tables) {
    const m = new Map(maps[t]);
    for (const r of rowsOf(bundle, t)) {
      const err = missingFieldsError(t, r as RowOf<typeof t>);
      // Máy chủ đã báo lỗi dòng này thì giữ lý do của máy chủ.
      if (err && m.get(r.rowNumber)?.action !== EXCEL_ROW_ACTION.ERROR)
        m.set(r.rowNumber, { action: EXCEL_ROW_ACTION.ERROR, error: err });
    }
    out[t] = m;
  }
  return out;
};

/** Số dòng lỗi (máy chủ báo, lỗi đọc file, thiếu trường) và lỗi chéo giữa các bảng của một file. */
export const bundleErrorCount = (bundle: ExcelBundle, check: ExcelBundleCheck) => {
  const maps = withMissingFields(statusMaps(check), bundle, REVIEW_TABLES);
  let rows = 0;
  for (const t of REVIEW_TABLES)
    for (const r of rowsOf(bundle, t)) {
      const parseError = 'parseError' in r ? r.parseError : null;
      if (rowStateOf(maps[t].get(r.rowNumber), parseError) === 'error') rows++;
    }
  return { rows, cross: check.spec.crossErrors?.length ?? 0 };
};

/** Phân loại một dòng: lỗi định dạng lúc đọc file cũng là lỗi (khi chưa có kết quả kiểm). */
export const rowStateOf = (status: RowStatus | undefined, parseError?: string | null): RowState => {
  if (status?.action === EXCEL_ROW_ACTION.ERROR || (!status && parseError)) return 'error';
  if (status?.action === EXCEL_ROW_ACTION.NEW_MATERIAL) return 'newMaterial';
  return status?.action === EXCEL_ROW_ACTION.PENDING ? 'pending' : 'valid';
};

/** Giá trị mặc định theo loại khi đổi loại NVL mới (đơn vị, khổ). */
export const materialTypeDefaults = (materialType: string, widthM?: number | null) => {
  const fabric = materialType === MATERIAL_TYPE.FABRIC;
  const d = fabric ? NEW_MATERIAL_DEFAULTS.FABRIC : NEW_MATERIAL_DEFAULTS.TRIM;
  return {
    materialType,
    unit: d.unit,
    widthM: fabric ? (widthM ?? d.widthM) : null,
  };
};

/**
 * DEV-ALL-123: gợi ý NVL mới — tên, đơn vị lấy từ sheet NVL của đơn (nếu có); vải (dùng cho chi tiết PANEL) = vải chính, mét,
 * màu áo đầu tiên dùng mã, khổ mặc định; phụ liệu = đơn vị mặc định.
 */
export const defaultMaterial = (m: MissingMaterial): SaveMaterialPayload => {
  const fabric = m.suggestedType === MATERIAL_TYPE.FABRIC;
  const base = materialTypeDefaults(fabric ? MATERIAL_TYPE.FABRIC : MATERIAL_TYPE.TRIM);
  return {
    ...base,
    materialCode: m.materialCode,
    materialName: m.suggestedName ?? '',
    unit: fabric ? base.unit : m.suggestedUnit || base.unit,
    color: fabric ? (m.colors[0] ?? '') : '',
  };
};

/** NVL mới còn thiếu thông tin bắt buộc (tên; vải cần khổ; phụ liệu cần đơn vị). */
export const incompleteMaterials = (materials: SaveMaterialPayload[]) =>
  materials.filter(
    (m) =>
      !m.materialName.trim() ||
      (m.materialType === MATERIAL_TYPE.FABRIC ? !m.widthM : !m.unit.trim()),
  );
