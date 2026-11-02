import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import type { ProductSpec, SaveProductSpecPayload } from '../types';
import {
  MAX_SIZE_CM,
  newKey,
  type ComponentRow,
  type MaterialOption,
  type NormRow,
  type VariantRow,
} from './productSpec';

const text = MESSAGES.orders.specValidation;

/** Số bìa tối đa của một mã hàng, khớp SaveProductSpecDtoValidator ở BE. */
export const MAX_MARKERS = 20;

export type MarkerSize = { lengthCm: number | null; widthCm: number | null };

/**
 * Một bìa trên màn hình: kích thước theo size và vải theo màu áo. Số bìa là vị trí trong danh sách, không lưu trong dòng,
 * nên xóa một bìa thì các bìa sau tự dồn số. Khóa của `sizes` và `fabrics` là `codeKey`.
 */
export type MarkerRow = {
  key: string;
  sizes: Record<string, MarkerSize>;
  fabrics: Record<string, number | undefined>;
};

/** Khóa so khớp size và màu: không phân biệt hoa thường, khoảng trắng đầu cuối. */
export const codeKey = (value: string) => value.trim().toUpperCase();

export const emptyMarker = (): MarkerRow => ({ key: newKey(), sizes: {}, fabrics: {} });

/** Các giá trị khác nhau của một cột màu hoặc size, giữ cách viết gốc để hiển thị. */
const distinctBy = (values: string[]): string[] => [
  ...new Map(values.map((v) => [codeKey(v), v.trim()])).values(),
];

export const distinctSizes = (variants: VariantRow[]) =>
  distinctBy(variants.map((v) => v.sizeCode));
export const distinctColors = (variants: VariantRow[]) => distinctBy(variants.map((v) => v.color));

/** Gom dòng máy chủ thành từng bìa, xếp theo số bìa. Chỉ giữ size và màu của màu – size còn dùng. */
export function toMarkerRows(spec: ProductSpec, variants: VariantRow[]): MarkerRow[] {
  const sizes = new Set(variants.map((v) => codeKey(v.sizeCode)));
  const colors = new Set(variants.map((v) => codeKey(v.color)));
  const byNo = new Map<number, MarkerRow>();
  const rowOf = (no: number): MarkerRow => {
    const row = byNo.get(no) ?? emptyMarker();
    byNo.set(no, row);
    return row;
  };
  for (const m of spec.markers) {
    if (sizes.has(codeKey(m.sizeCode)))
      rowOf(m.markerNo).sizes[codeKey(m.sizeCode)] = { lengthCm: m.lengthCm, widthCm: m.widthCm };
  }
  for (const f of spec.markerFabrics) {
    if (colors.has(codeKey(f.color))) rowOf(f.markerNo).fabrics[codeKey(f.color)] = f.materialId;
  }
  return [...byNo.entries()].sort(([a], [b]) => a - b).map(([, row]) => row);
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

const sizeIsValid = (value: number) => value > 0 && value <= MAX_SIZE_CM;

/** Lỗi nhập một bìa; chuỗi rỗng = hợp lệ. `no` là số bìa hiển thị. */
function markerProblem(
  marker: MarkerRow,
  no: number,
  variants: VariantRow[],
  allowedFabrics: (color: string) => Set<number>,
): string {
  const entries = Object.values(marker.sizes).filter(
    (s) => s.lengthCm !== null || s.widthCm !== null,
  );
  if (entries.some((s) => s.lengthCm === null || s.widthCm === null))
    return fill(text.markerSizePair, no);
  if (entries.some((s) => !sizeIsValid(s.lengthCm ?? 0) || !sizeIsValid(s.widthCm ?? 0)))
    return fill(text.markerSizeRange, no);

  const fabrics = distinctColors(variants).filter((c) => marker.fabrics[codeKey(c)] !== undefined);
  if (entries.length === 0 || fabrics.length === 0) return fill(text.markerIncomplete, no);
  const wrong = fabrics.find((c) => !allowedFabrics(c).has(marker.fabrics[codeKey(c)] as number));
  return wrong === undefined ? '' : fill(text.markerFabricNotInNorm, no, wrong);
}

/** Lỗi nhập bìa (hiện cho người dùng trước khi gửi); chuỗi rỗng = hợp lệ. */
export function validateMarkers(
  markers: MarkerRow[],
  variants: VariantRow[],
  norms: NormRow[],
  components: ComponentRow[],
  materialOptions: MaterialOption[],
): string {
  if (markers.length > MAX_MARKERS) return fill(text.maxMarkers, MAX_MARKERS);
  const allowedFabrics = (color: string) =>
    new Set(fabricOptionsFor(color, norms, components, materialOptions).map((o) => o.value));
  for (const [index, marker] of markers.entries()) {
    const problem = markerProblem(marker, index + 1, variants, allowedFabrics);
    if (problem) return problem;
  }
  return '';
}

/** Mỗi bìa nở ra từng size và từng màu đã nhập; số bìa lấy theo vị trí trong danh sách. */
export function toMarkerPayload(
  markers: MarkerRow[],
  variants: VariantRow[],
): Pick<SaveProductSpecPayload, 'markers' | 'markerFabrics'> {
  const sizes = distinctSizes(variants);
  const colors = distinctColors(variants);
  return {
    markers: markers.flatMap((m, index) =>
      sizes.flatMap((sizeCode) => {
        const size = m.sizes[codeKey(sizeCode)];
        return size?.lengthCm && size.widthCm
          ? [{ markerNo: index + 1, sizeCode, lengthCm: size.lengthCm, widthCm: size.widthCm }]
          : [];
      }),
    ),
    markerFabrics: markers.flatMap((m, index) =>
      colors.flatMap((color) => {
        const materialId = m.fabrics[codeKey(color)];
        return materialId === undefined ? [] : [{ markerNo: index + 1, color, materialId }];
      }),
    ),
  };
}
