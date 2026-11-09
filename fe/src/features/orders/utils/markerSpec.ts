import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import type { ProductSpec, SaveProductSpecPayload } from '../types';
import {
  newKey,
  type ComponentRow,
  type MaterialOption,
  type NormRow,
  type VariantRow,
} from './productSpec';

const text = MESSAGES.orders.specValidation;

/** Số nhóm bìa tối đa của một mã hàng, khớp SaveProductSpecDtoValidator ở BE (số nhóm 1..20). */
export const MAX_MARKERS = 20;

/**
 * Một nhóm bìa trên màn hình (DEV-ALL-85): các chi tiết cắt (Panel) cắt chung một bìa và vải theo màu áo.
 * Bìa cụ thể (mã, dài, tỉ lệ size) do đơn hàng khai. Số nhóm giữ cố định khi xóa nhóm khác, vì bìa của đơn trỏ theo số nhóm.
 * Khóa của `fabrics` là `codeKey(màu)`; `componentKeys` là `ComponentRow.key`.
 */
export type MarkerRow = {
  key: string;
  markerNo: number;
  componentKeys: string[];
  fabrics: Record<string, number | undefined>;
};

/** Khóa so khớp size và màu: không phân biệt hoa thường, khoảng trắng đầu cuối. */
export const codeKey = (value: string) => value.trim().toUpperCase();

/** Nhóm mới lấy số nhỏ nhất chưa dùng. */
export const emptyMarker = (markers: MarkerRow[]): MarkerRow => {
  const used = new Set(markers.map((m) => m.markerNo));
  let markerNo = 1;
  while (used.has(markerNo)) markerNo++;
  return { key: newKey(), markerNo, componentKeys: [], fabrics: {} };
};

/** Các giá trị khác nhau của một cột màu hoặc size, giữ cách viết gốc để hiển thị. */
const distinctBy = (values: string[]): string[] => [
  ...new Map(values.map((v) => [codeKey(v), v.trim()])).values(),
];

export const distinctColors = (variants: VariantRow[]) => distinctBy(variants.map((v) => v.color));

/** Gom dòng máy chủ thành từng nhóm bìa, xếp theo số nhóm. Chỉ giữ chi tiết còn dùng và màu còn trong màu – size. */
export function toMarkerRows(
  spec: ProductSpec,
  variants: VariantRow[],
  components: ComponentRow[],
): MarkerRow[] {
  const colors = new Set(variants.map((v) => codeKey(v.color)));
  const keyByName = new Map(components.map((c) => [codeKey(c.componentName), c.key]));
  const byNo = new Map<number, MarkerRow>();
  const rowOf = (markerNo: number): MarkerRow => {
    const row = byNo.get(markerNo) ?? { key: newKey(), markerNo, componentKeys: [], fabrics: {} };
    byNo.set(markerNo, row);
    return row;
  };
  for (const m of spec.markerComponents) {
    const key = keyByName.get(codeKey(m.componentName));
    if (key !== undefined) rowOf(m.markerNo).componentKeys.push(key);
  }
  for (const f of spec.markerFabrics) {
    if (colors.has(codeKey(f.color))) rowOf(f.markerNo).fabrics[codeKey(f.color)] = f.materialId;
  }
  return [...byNo.values()].sort((a, b) => a.markerNo - b.markerNo);
}

/** Vải chọn được cho một màu: NVL loại vải có trong định mức chi tiết cắt của màu đó, khớp kiểm tra ở BE. */
export function fabricOptionsFor(
  color: string,
  norms: NormRow[],
  components: ComponentRow[],
  materialOptions: MaterialOption[],
): MaterialOption[] {
  const panels = new Set(components.filter((c) => c.componentType === 'Panel').map((c) => c.key));
  const prefix = `${codeKey(color)}|`;
  const used = new Set(
    norms
      .filter(
        (n) =>
          n.componentKey !== undefined &&
          panels.has(n.componentKey) &&
          n.variantKeys.some((k) => k.startsWith(prefix)),
      )
      .map((n) => n.materialId),
  );
  return materialOptions.filter((m) => m.isFabric && used.has(m.value));
}

/** Lỗi nhập một nhóm; chuỗi rỗng = hợp lệ. */
function markerProblem(
  marker: MarkerRow,
  colors: string[],
  panelKeys: Set<string>,
  allowedFabrics: (color: string) => Set<number>,
): string {
  const no = marker.markerNo;
  const fabrics = colors.filter((c) => marker.fabrics[codeKey(c)] !== undefined);
  const components = marker.componentKeys.filter((k) => panelKeys.has(k));
  if (components.length === 0 || fabrics.length === 0) return fill(text.markerIncomplete, no);
  const wrong = fabrics.find((c) => !allowedFabrics(c).has(marker.fabrics[codeKey(c)] as number));
  return wrong === undefined ? '' : fill(text.markerFabricNotInNorm, no, wrong);
}

/** Lỗi nhập nhóm bìa (hiện cho người dùng trước khi gửi); chuỗi rỗng = hợp lệ. */
export function validateMarkers(
  markers: MarkerRow[],
  variants: VariantRow[],
  norms: NormRow[],
  components: ComponentRow[],
  materialOptions: MaterialOption[],
): string {
  if (markers.length > MAX_MARKERS) return fill(text.maxMarkers, MAX_MARKERS);
  const panels = components.filter((c) => c.componentType === 'Panel');
  const panelKeys = new Set(panels.map((c) => c.key));
  const colors = distinctColors(variants);
  const allowedFabrics = (color: string) =>
    new Set(fabricOptionsFor(color, norms, components, materialOptions).map((o) => o.value));
  for (const marker of markers) {
    const problem = markerProblem(marker, colors, panelKeys, allowedFabrics);
    if (problem) return problem;
  }
  if (markers.length === 0) return '';
  const grouped = new Set(markers.flatMap((m) => m.componentKeys));
  const missing = panels.find((c) => !grouped.has(c.key));
  return missing ? fill(text.panelWithoutMarker, missing.componentName.trim()) : '';
}

/** Mỗi nhóm nở ra từng chi tiết và từng màu đã chọn vải. Chi tiết đã xóa hoặc đổi thành phụ liệu thì bỏ. */
export function toMarkerPayload(
  markers: MarkerRow[],
  variants: VariantRow[],
  components: ComponentRow[],
): Pick<SaveProductSpecPayload, 'markerComponents' | 'markerFabrics'> {
  const colors = distinctColors(variants);
  const panelName = new Map(
    components
      .filter((c) => c.componentType === 'Panel')
      .map((c) => [c.key, c.componentName.trim()]),
  );
  return {
    markerComponents: markers.flatMap((m) =>
      m.componentKeys.flatMap((k) => {
        const componentName = panelName.get(k);
        return componentName === undefined ? [] : [{ markerNo: m.markerNo, componentName }];
      }),
    ),
    markerFabrics: markers.flatMap((m) =>
      colors.flatMap((color) => {
        const materialId = m.fabrics[codeKey(color)];
        return materialId === undefined ? [] : [{ markerNo: m.markerNo, color, materialId }];
      }),
    ),
  };
}
