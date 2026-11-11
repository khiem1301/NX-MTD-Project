import type { ProductSpec, SaveProductSpecPayload } from '../types';
import {
  colorKey,
  newKey,
  type ComponentRow,
  type MaterialOption,
  type NormRow,
  type VariantRow,
} from './productSpec';

/** Số nhóm bìa tối đa của một mã hàng, khớp SaveProductSpecDtoValidator ở BE (số nhóm 1..20). */
export const MAX_MARKERS = 20;

/** Một nhóm bìa: các chi tiết cắt (Panel) cắt chung một bìa. Số nhóm giữ cố định khi xóa nhóm khác (bìa của đơn trỏ theo số nhóm). */
export type GroupRow = { key: string; markerNo: number; componentKeys: string[] };

/**
 * Một dạng bìa (DEV-ALL-89): các màu áo dùng chung đúng một bộ nhóm bìa; ở mỗi màu, mỗi nhóm chỉ một vải. Màu có nhóm mà chi tiết
 * khác vải thì cả màu sang dạng bìa riêng. Vải của nhóm ở mỗi màu lấy từ định mức, không nhập.
 * `colorKeys` là `colorKey(màu)`, `componentKeys` là `ComponentRow.key`.
 */
export type LayoutRow = { key: string; colorKeys: string[]; groups: GroupRow[] };

/** Vải của chi tiết cắt theo màu áo: khóa ngoài là `ComponentRow.key`, khóa trong là `colorKey(màu)`, giá trị là MaterialId. */
export type PanelFabrics = Map<string, Map<string, number>>;

/** Tên hiển thị của dạng bìa thứ `index` (A, B, C...). */
export const layoutName = (index: number) => String.fromCharCode(65 + index);

/** Các giá trị khác nhau của một cột màu hoặc size, giữ cách viết gốc để hiển thị. */
const distinctBy = (values: string[]): string[] => [
  ...new Map(values.map((v) => [colorKey(v), v.trim()])).values(),
];

export const distinctColors = (variants: VariantRow[]) => distinctBy(variants.map((v) => v.color));

/** Dạng bìa mới: lấy các màu chưa thuộc dạng nào, chưa có nhóm. */
export const emptyLayout = (layouts: LayoutRow[], variants: VariantRow[]): LayoutRow => {
  const used = new Set(layouts.flatMap((l) => l.colorKeys));
  const colorKeys = distinctColors(variants)
    .map(colorKey)
    .filter((ck) => !used.has(ck));
  return { key: newKey(), colorKeys, groups: [] };
};

/** Nhóm mới lấy số nhỏ nhất chưa dùng ở mọi dạng bìa của mã hàng. */
export const emptyGroup = (layouts: LayoutRow[], componentKeys: string[] = []): GroupRow => {
  const used = new Set(layouts.flatMap((l) => l.groups.map((g) => g.markerNo)));
  let markerNo = 1;
  while (used.has(markerNo)) markerNo++;
  return { key: newKey(), markerNo, componentKeys };
};

/** Gom dòng máy chủ thành dạng bìa: các nhóm có cùng tập màu dùng nhóm là một dạng. Chỉ giữ chi tiết còn dùng và màu còn trong màu – size. */
export function toLayoutRows(
  spec: ProductSpec,
  variants: VariantRow[],
  components: ComponentRow[],
): LayoutRow[] {
  const colors = new Set(variants.map((v) => colorKey(v.color)));
  const keyByName = new Map(components.map((c) => [colorKey(c.componentName), c.key]));
  const groups = new Map<number, GroupRow & { colorKeys: string[] }>();
  const groupOf = (markerNo: number) => {
    const g = groups.get(markerNo) ?? { key: newKey(), markerNo, componentKeys: [], colorKeys: [] };
    groups.set(markerNo, g);
    return g;
  };
  for (const m of spec.markerComponents) {
    const key = keyByName.get(colorKey(m.componentName));
    if (key !== undefined) groupOf(m.markerNo).componentKeys.push(key);
  }
  for (const f of spec.markerFabrics) {
    if (colors.has(colorKey(f.color))) groupOf(f.markerNo).colorKeys.push(colorKey(f.color));
  }
  const layouts = new Map<string, LayoutRow>();
  for (const { colorKeys, ...group } of [...groups.values()].sort(
    (a, b) => a.markerNo - b.markerNo,
  )) {
    const id = [...colorKeys].sort().join('|');
    const layout = layouts.get(id) ?? { key: newKey(), colorKeys, groups: [] };
    layout.groups.push(group);
    layouts.set(id, layout);
  }
  return [...layouts.values()];
}

/** Vải (NVL loại vải) của từng chi tiết cắt theo màu áo, lấy từ định mức. Mỗi chi tiết cắt chỉ một vải ở một màu (DEV-ALL-86). */
export function panelFabrics(
  norms: NormRow[],
  components: ComponentRow[],
  materialOptions: MaterialOption[],
): PanelFabrics {
  const panels = new Set(components.filter((c) => c.componentType === 'Panel').map((c) => c.key));
  const fabrics = new Set(materialOptions.filter((m) => m.isFabric).map((m) => m.value));
  const result: PanelFabrics = new Map();
  for (const n of norms) {
    if (n.componentKey === undefined || n.materialId === undefined) continue;
    if (!panels.has(n.componentKey) || !fabrics.has(n.materialId)) continue;
    const byColor = result.get(n.componentKey) ?? new Map<string, number>();
    for (const ck of n.colorKeys) if (!byColor.has(ck)) byColor.set(ck, n.materialId);
    result.set(n.componentKey, byColor);
  }
  return result;
}

/** Các vải khác nhau mà chi tiết của nhóm dùng ở một màu áo; hợp lệ khi đúng một vải. */
export const markerFabricsOf = (
  componentKeys: string[],
  ck: string,
  fabrics: PanelFabrics,
): number[] => [
  ...new Set(
    componentKeys.flatMap((k) => {
      const id = fabrics.get(k)?.get(ck);
      return id === undefined ? [] : [id];
    }),
  ),
];

/** Các nhóm chi tiết cùng vải của một màu (không xét vải cụ thể), dùng để gợi ý nhóm bìa cho dạng mới. */
const fabricSplit = (ck: string, fabrics: PanelFabrics): string[][] => {
  const byFabric = new Map<number, string[]>();
  for (const [panel, byColor] of fabrics) {
    const id = byColor.get(ck);
    if (id !== undefined) byFabric.set(id, [...(byFabric.get(id) ?? []), panel]);
  }
  return [...byFabric.values()]
    .map((keys) => [...keys].sort())
    .sort((a, b) => a.join().localeCompare(b.join()));
};

/** Màu dùng được mọi nhóm của dạng: mỗi nhóm đúng một vải ở màu đó và mỗi chi tiết có vải nằm đúng một nhóm. */
export function fitsLayout(layout: LayoutRow, ck: string, fabrics: PanelFabrics): boolean {
  if (layout.groups.some((g) => markerFabricsOf(g.componentKeys, ck, fabrics).length !== 1))
    return false;
  return [...fabrics].every(
    ([panel, byColor]) =>
      byColor.get(ck) === undefined ||
      layout.groups.filter((g) => g.componentKeys.includes(panel)).length === 1,
  );
}

/**
 * Gợi ý dạng bìa từ định mức: màu dùng được mọi nhóm của một dạng đã có thì vào dạng đó; không thì tạo dạng mới, mỗi nhóm là các
 * chi tiết cùng vải ở màu đó.
 */
export function suggestLayouts(variants: VariantRow[], fabrics: PanelFabrics): LayoutRow[] {
  const layouts: LayoutRow[] = [];
  for (const ck of distinctColors(variants).map(colorKey)) {
    const split = fabricSplit(ck, fabrics);
    if (split.length === 0) continue;
    const fit = layouts.find((l) => fitsLayout(l, ck, fabrics));
    if (fit) {
      fit.colorKeys.push(ck);
      continue;
    }
    const layout: LayoutRow = { key: newKey(), colorKeys: [ck], groups: [] };
    for (const keys of split) layout.groups.push(emptyGroup([...layouts, layout], keys));
    layouts.push(layout);
  }
  return layouts;
}

/**
 * Lý do chi tiết không chọn được cho nhóm: đã ở nhóm khác của dạng, hoặc khác vải với chi tiết đã chọn ở một màu của dạng
 * (một bìa chỉ trải trên một vải). Undefined khi chọn được.
 */
export function componentBlock(
  layout: LayoutRow,
  group: GroupRow,
  componentKey: string,
  fabrics: PanelFabrics,
): { otherGroup: number } | { colorKey: string } | undefined {
  const other = layout.groups.find(
    (g) => g.key !== group.key && g.componentKeys.includes(componentKey),
  );
  if (other) return { otherGroup: other.markerNo };
  const others = group.componentKeys.filter((k) => k !== componentKey);
  const ck = layout.colorKeys.find((c) => {
    const own = fabrics.get(componentKey)?.get(c);
    return own !== undefined && markerFabricsOf(others, c, fabrics).some((id) => id !== own);
  });
  return ck === undefined ? undefined : { colorKey: ck };
}

/** Mỗi nhóm nở ra từng chi tiết và từng màu của dạng bìa. Chi tiết đã xóa hoặc đổi thành phụ liệu, màu không còn thì bỏ. */
export function toMarkerPayload(
  layouts: LayoutRow[],
  variants: VariantRow[],
  components: ComponentRow[],
): Pick<SaveProductSpecPayload, 'markerComponents' | 'markerColors'> {
  const names = new Map(distinctColors(variants).map((c) => [colorKey(c), c]));
  const panelName = new Map(
    components
      .filter((c) => c.componentType === 'Panel')
      .map((c) => [c.key, c.componentName.trim()]),
  );
  const groups = layouts.flatMap((l) => l.groups.map((g) => ({ g, colorKeys: l.colorKeys })));
  return {
    markerComponents: groups.flatMap(({ g }) =>
      g.componentKeys.flatMap((k) => {
        const componentName = panelName.get(k);
        return componentName === undefined ? [] : [{ markerNo: g.markerNo, componentName }];
      }),
    ),
    markerColors: groups.flatMap(({ g, colorKeys }) =>
      colorKeys.flatMap((ck) => {
        const color = names.get(ck);
        return color === undefined ? [] : [{ markerNo: g.markerNo, color }];
      }),
    ),
  };
}
