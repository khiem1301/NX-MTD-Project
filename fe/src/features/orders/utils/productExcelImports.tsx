import { Modal, Space, Tag, Typography } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { queryClient } from '@/services/queryClient';
import { isApiError } from '@/utils/apiError';
import { fill } from '@/utils/format';
import { materialApi } from '@/services/materialApi';
import type { SaveMaterialPayload } from '@/types/material';
import { excelBundleApi } from '../api/orderApi';
import type { ExcelBundle, SpecImportPreview, SpecWorkbook } from '../types';

const text = MESSAGES.orders.productExcel;
const MAX_ROW_ERRORS = 5;
const RESULT_WIDTH = 640;

/** DEV-ALL-121: các mục nhập Excel ở form mã hàng, theo thứ tự nhập (thành phần trước vì định mức, nhóm bìa dựa vào nó). */
export type ProductImportKind = 'components' | 'norms' | 'groups';
export const PRODUCT_IMPORT_KINDS: readonly ProductImportKind[] = ['components', 'norms', 'groups'];

/**
 * DEV-ALL-122: dữ liệu đã đọc từ file Excel và sửa trên popup kiểm tra (không còn giữ file). Chỉ được ghi khi bấm Lưu mã hàng
 * (hoặc Lưu đơn ở form đơn).
 */
export type ProductImportSelection = {
  bundle?: ExcelBundle;
  /** Tên file đã tải (hiện trên khung nhập). */
  fileName?: string;
  /**
   * DEV-ALL-123: NVL chưa có trong danh mục mà admin chọn "Thêm vào danh mục" khi Xác nhận popup. Tạo trước khi nhập (lúc Lưu);
   * rỗng / không có = không thêm, các dòng dùng NVL đó bị bỏ qua khi lưu.
   */
  newMaterials?: SaveMaterialPayload[];
};

export type ProductImportOutcome = {
  kind: ProductImportKind | 'materials';
  ok: boolean;
  message: string;
  rowErrors: string[];
};

export const EMPTY_WORKBOOK: SpecWorkbook = {
  hasComponents: false,
  components: [],
  hasNorms: false,
  norms: [],
  hasMarkerGroups: false,
  markerGroups: [],
  hasOrderMarkers: false,
  orderMarkers: [],
  sizeColumns: [],
};
export const EMPTY_BUNDLE: ExcelBundle = {
  hasProducts: false,
  products: [],
  spec: EMPTY_WORKBOOK,
  hasOrderMaterials: false,
  orderMaterials: [],
};

/** Số dòng của từng mục trong bộ dữ liệu. */
export const rowCountOf = (bundle: ExcelBundle, kind: ProductImportKind): number => {
  switch (kind) {
    case 'components':
      return bundle.spec.components.length;
    case 'norms':
      return bundle.spec.norms.length;
    case 'groups':
      return bundle.spec.markerGroups.length;
  }
};

/** Mục có dữ liệu để nhập. */
export const kindsWithData = (bundle: ExcelBundle | undefined): ProductImportKind[] =>
  bundle ? PRODUCT_IMPORT_KINDS.filter((k) => rowCountOf(bundle, k) > 0) : [];

export const hasImportFiles = (s: ProductImportSelection) => kindsWithData(s.bundle).length > 0;

/**
 * DEV-ALL-124: mục Excel bắt buộc khi Thêm mới mã hàng: Thành phần, Định mức NVL, Nhóm bìa. Sửa mã hàng: không bắt buộc.
 */
export const requiredKindsFor = (creating: boolean): readonly ProductImportKind[] =>
  creating ? PRODUCT_IMPORT_KINDS : [];

/** Mục bắt buộc mà bộ dữ liệu chưa có dòng nào. */
export const missingKinds = (
  selection: ProductImportSelection,
  required: readonly ProductImportKind[],
): ProductImportKind[] => {
  const present = kindsWithData(selection.bundle);
  return required.filter((k) => !present.includes(k));
};

/** Bỏ dữ liệu một mục (nút ✕ trên khung nhập). */
export const clearKind = (bundle: ExcelBundle, kind: ProductImportKind): ExcelBundle => {
  const spec = bundle.spec;
  switch (kind) {
    case 'components':
      return { ...bundle, spec: { ...spec, hasComponents: false, components: [] } };
    case 'norms':
      return { ...bundle, spec: { ...spec, hasNorms: false, norms: [] } };
    case 'groups':
      return { ...bundle, spec: { ...spec, hasMarkerGroups: false, markerGroups: [] } };
  }
};

/** File chung có thể kèm sheet Bìa của đơn, NVL của đơn: form mã hàng không nhập hai bảng này nên bỏ ngay khi đọc file. */
export const withoutOrderData = (bundle: ExcelBundle): ExcelBundle => ({
  ...bundle,
  hasOrderMaterials: false,
  orderMaterials: [],
  spec: { ...bundle.spec, hasOrderMarkers: false, orderMarkers: [], sizeColumns: [] },
});

/** Chọn file cho một mục: lấy đúng bảng của mục đó từ file mới, các mục khác giữ nguyên. */
export const takeKind = (
  base: ExcelBundle,
  incoming: ExcelBundle,
  kind: ProductImportKind,
): ExcelBundle => {
  const spec = base.spec;
  const from = incoming.spec;
  switch (kind) {
    case 'components':
      return {
        ...base,
        spec: { ...spec, hasComponents: from.hasComponents, components: from.components },
      };
    case 'norms':
      return { ...base, spec: { ...spec, hasNorms: from.hasNorms, norms: from.norms } };
    case 'groups':
      return {
        ...base,
        spec: { ...spec, hasMarkerGroups: from.hasMarkerGroups, markerGroups: from.markerGroups },
      };
  }
};

/** Bảng định mức chỉ gồm bảng của một mục (nhập riêng từng mục, theo thứ tự). */
const workbookOf = (spec: SpecWorkbook, part: ProductImportKind): SpecWorkbook => ({
  ...EMPTY_WORKBOOK,
  ...(part === 'components' && { hasComponents: true, components: spec.components }),
  ...(part === 'norms' && { hasNorms: true, norms: spec.norms }),
  ...(part === 'groups' && { hasMarkerGroups: true, markerGroups: spec.markerGroups }),
});

type ErrorRow = { rowNumber: number; error: string | null };

const rowErrorsOf = (rows: readonly ErrorRow[]): string[] => {
  const errors = rows.filter((r) => r.error);
  const lines = errors
    .slice(0, MAX_ROW_ERRORS)
    .map((r) => fill(text.rowError, r.rowNumber, r.error ?? ''));
  if (errors.length > MAX_ROW_ERRORS)
    lines.push(fill(text.moreErrors, errors.length - MAX_ROW_ERRORS));
  return lines;
};

const specRows = (preview: SpecImportPreview, part: ProductImportKind): readonly ErrorRow[] => {
  switch (part) {
    case 'components':
      return preview.components;
    case 'norms':
      return preview.norms;
    case 'groups':
      return preview.markerGroups;
  }
};

/** Một bảng định mức: xem trước (lấy dòng lỗi) rồi nhập nếu có thay đổi hợp lệ. */
const importSpecPart = async (
  productId: number,
  part: ProductImportKind,
  spec: SpecWorkbook,
): Promise<Omit<ProductImportOutcome, 'kind'>> => {
  const workbook = workbookOf(spec, part);
  const preview = await excelBundleApi.previewSpec(productId, workbook, undefined, [part]);
  // Ghi chú của máy chủ (vd màu bị bỏ qua ở nhóm bìa) hiện chung với dòng lỗi.
  const rowErrors = [...rowErrorsOf(specRows(preview, part)), ...preview.notes];
  if (!preview.canImport)
    return { ok: false, message: preview.crossErrors[0] ?? text.nothing, rowErrors };
  const result = await excelBundleApi.importSpec(productId, workbook, undefined, [part]);
  return { ok: true, message: result.message ?? MESSAGES.common.saved, rowErrors };
};

/** DEV-ALL-123: tạo NVL mới vào danh mục (API Admin) trước khi nhập; NVL lỗi (vd mã vừa có người tạo) báo riêng, không chặn. */
const createMaterials = async (materials: SaveMaterialPayload[]): Promise<ProductImportOutcome> => {
  const created: string[] = [];
  const failed: string[] = [];
  for (const m of materials) {
    try {
      await materialApi.create('admin', m);
      created.push(m.materialName ?? '');
    } catch (error) {
      failed.push(
        fill(
          text.materialFailed,
          m.materialName ?? '',
          isApiError(error) ? error.message : MESSAGES.common.unknownError,
        ),
      );
    }
  }
  return {
    kind: 'materials',
    ok: created.length > 0 && failed.length === 0,
    message: fill(text.materialsCreated, created.length, created.join(', ') || '—'),
    rowErrors: failed,
  };
};

/**
 * Nhập lần lượt dữ liệu đã kiểm cho mã hàng vừa lưu. Mục lỗi không chặn mục sau (mỗi mục là một lần nhập riêng của máy chủ).
 * Xong thì làm mới dữ liệu đơn hàng, tra cứu, kho, cắt.
 */
export async function runProductImports(
  productId: number,
  selection: ProductImportSelection,
): Promise<ProductImportOutcome[]> {
  const bundle = selection.bundle;
  const outcomes: ProductImportOutcome[] = [];
  if (!bundle) return outcomes;
  if (selection.newMaterials?.length) outcomes.push(await createMaterials(selection.newMaterials));
  for (const kind of kindsWithData(bundle)) {
    try {
      const result = await importSpecPart(productId, kind, bundle.spec);
      outcomes.push({ kind, ...result });
    } catch (error) {
      outcomes.push({
        kind,
        ok: false,
        message: isApiError(error) ? error.message : MESSAGES.common.unknownError,
        rowErrors: [],
      });
    }
  }
  if (outcomes.some((o) => o.ok) || (selection.newMaterials?.length ?? 0) > 0)
    await Promise.all(
      [['orders'], ['lookups'], ['order-lookups'], ['warehouse'], ['cutting']].map((queryKey) =>
        queryClient.invalidateQueries({ queryKey }),
      ),
    );
  return outcomes;
}

/** Hộp thoại kết quả: mỗi mục một dòng (Đã nhập / Chưa nhập / Bỏ qua) kèm lỗi của từng dòng. */
export function showProductImportResults(productCode: string, outcomes: ProductImportOutcome[]) {
  const show = outcomes.every((o) => o.ok) ? Modal.success : Modal.warning;
  show({
    title: text.resultTitle,
    width: RESULT_WIDTH,
    okText: MESSAGES.common.close,
    content: (
      <Space direction="vertical" size={12} style={{ width: '100%' }}>
        <Typography.Text>{fill(text.saved, productCode)}</Typography.Text>
        {outcomes.map((o) => (
          <div key={o.kind}>
            <Space size={8} wrap>
              <Tag color={o.ok ? 'green' : 'red'}>{text.kinds[o.kind]}</Tag>
              <Typography.Text type={o.ok ? undefined : 'danger'}>{o.message}</Typography.Text>
            </Space>
            {o.rowErrors.map((line) => (
              <div key={line}>
                <Typography.Text type="secondary">{line}</Typography.Text>
              </div>
            ))}
          </div>
        ))}
      </Space>
    ),
  });
}
