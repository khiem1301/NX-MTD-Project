import type { OrderMaterial } from '@/features/warehouse/types';
import { EXCEL_COMPONENT_TYPE, EXCEL_MARKER_STATUS } from '../constants';
import type { ExcelBundle, OrderMarkerProduct, ProductSpec } from '../types';
import { REVIEW_TABLES, rowsOf, type ReviewTable } from './excelReview';

/** Dữ liệu đang có trong hệ thống để nạp vào bảng sửa của popup kiểm tra Excel (bảng nào không có dữ liệu thì bỏ trống). */
export type ExistingData = {
  spec?: ProductSpec;
  /** Bìa của đúng mã hàng đang nhập, trong đơn đang chọn. */
  markers?: OrderMarkerProduct;
  orderMaterials?: OrderMaterial[];
};

const up = (v: string) => v.trim().toUpperCase();

/**
 * Nạp dữ liệu đang có vào bản nháp để sửa trực tiếp, không cần bật "Thay thế". Chỉ thêm dòng mà file / bản nháp chưa có
 * (cùng khóa thì giữ dòng của file, vì khi nhập dòng của file ghi đè dòng cũ): thành phần theo tên, định mức theo cặp
 * màu + chi tiết, nhóm bìa theo số nhóm, bìa theo nhóm + mã bìa, NVL của đơn theo mã NVL. Dòng mới đánh số tiếp sau mọi số dòng
 * đang có để không trùng kết quả kiểm. Bảng được nạp thì coi như có trong file (hiện thành tab, được nhập).
 */
export const mergeExisting = (
  bundle: ExcelBundle,
  data: ExistingData,
  tables: readonly ReviewTable[],
): { bundle: ExcelBundle; loaded: ReviewTable[] } => {
  let next = Math.max(0, ...REVIEW_TABLES.flatMap((t) => rowsOf(bundle, t).map((r) => r.rowNumber))) + 1;
  const loaded: ReviewTable[] = [];
  let spec = bundle.spec;
  let orderMaterials = bundle.orderMaterials;
  let hasOrderMaterials = bundle.hasOrderMaterials;

  if (tables.includes('components') && data.spec) {
    const have = new Set(spec.components.map((c) => up(c.name)));
    const rows = data.spec.components
      .filter((c) => c.status === 'Active' && !have.has(up(c.componentName)))
      .map((c) => ({
        rowNumber: next++,
        name: c.componentName,
        componentType: c.componentType === 'Trim' ? EXCEL_COMPONENT_TYPE.TRIM : EXCEL_COMPONENT_TYPE.PANEL,
        piecesPerGarment: c.piecesPerGarment,
        parseError: null,
      }));
    spec = { ...spec, hasComponents: true, components: [...spec.components, ...rows] };
    loaded.push('components');
  }
  if (tables.includes('norms') && data.spec) {
    const havePairs = new Set(spec.norms.map((n) => `${up(n.color)}|${up(n.componentName)}`));
    const rows = data.spec.norms
      .filter((n) => !havePairs.has(`${up(n.color)}|${up(n.componentName)}`))
      .map((n) => ({
        rowNumber: next++,
        color: n.color,
        componentName: n.componentName,
        materialCode: n.materialCode,
        specNote: n.specNote,
        parseError: null,
      }));
    spec = { ...spec, hasNorms: true, norms: [...spec.norms, ...rows] };
    loaded.push('norms');
  }
  if (tables.includes('groups') && data.spec) {
    const haveNos = new Set(spec.markerGroups.map((g) => g.markerNo));
    const nos = [...new Set(data.spec.markerComponents.map((m) => m.markerNo))]
      .filter((no) => !haveNos.has(no))
      .sort((a, b) => a - b);
    const rows = nos.map((no) => ({
      rowNumber: next++,
      markerNo: no,
      componentNames: data.spec!.markerComponents.filter((m) => m.markerNo === no).map((m) => m.componentName),
      colors: [...new Set(data.spec!.markerFabrics.filter((f) => f.markerNo === no).map((f) => f.color))],
      parseError: null,
    }));
    spec = { ...spec, hasMarkerGroups: true, markerGroups: [...spec.markerGroups, ...rows] };
    loaded.push('groups');
  }
  if (tables.includes('markers') && data.markers) {
    const haveKeys = new Set(spec.orderMarkers.map((m) => `${m.markerNo}|${up(m.markerCode)}`));
    const rows = data.markers.markers
      .filter((m) => !haveKeys.has(`${m.markerNo}|${up(m.markerCode)}`))
      .map((m) => ({
        rowNumber: next++,
        productCode: data.markers!.productCode,
        markerNo: m.markerNo,
        markerCode: m.markerCode,
        lengthM: m.lengthM,
        widthM: m.widthM,
        status: m.status === 'Discontinued' ? EXCEL_MARKER_STATUS.DISCONTINUED : EXCEL_MARKER_STATUS.ACTIVE,
        ratios: m.ratios,
        parseError: null,
      }));
    const sizes = [...new Set([...spec.sizeColumns, ...data.markers.sizes])];
    spec = { ...spec, hasOrderMarkers: true, orderMarkers: [...spec.orderMarkers, ...rows], sizeColumns: sizes };
    loaded.push('markers');
  }
  if (tables.includes('orderMaterials') && data.orderMaterials) {
    const have = new Set(orderMaterials.map((m) => up(m.materialCode)));
    const rows = data.orderMaterials
      .filter((m) => !have.has(up(m.materialCode)))
      .map((m) => ({
        rowNumber: next++,
        materialCode: m.materialCode,
        materialName: m.materialName,
        unit: m.unit,
        requiredQty: m.requiredQty,
        customerQty: m.customerQty,
        note: m.note,
        parseError: null,
      }));
    orderMaterials = [...orderMaterials, ...rows];
    hasOrderMaterials = true;
    loaded.push('orderMaterials');
  }
  return { bundle: { ...bundle, spec, orderMaterials, hasOrderMaterials }, loaded };
};
