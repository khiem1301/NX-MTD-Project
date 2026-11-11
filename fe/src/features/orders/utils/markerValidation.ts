import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import {
  distinctColors,
  layoutName,
  MAX_MARKERS,
  markerFabricsOf,
  panelFabrics,
  type LayoutRow,
  type PanelFabrics,
} from './markerSpec';
import {
  colorKey,
  type ComponentRow,
  type MaterialOption,
  type NormRow,
  type VariantRow,
} from './productSpec';

const text = MESSAGES.orders.specValidation;

type Context = {
  panels: ComponentRow[];
  fabrics: PanelFabrics;
  colorName: (ck: string) => string;
};

/** Mỗi nhóm có chi tiết và đúng một vải ở mỗi màu của dạng; chuỗi rỗng = hợp lệ. */
function groupProblem(layout: LayoutRow, ctx: Context): string {
  const panelKeys = new Set(ctx.panels.map((p) => p.key));
  for (const group of layout.groups) {
    const no = group.markerNo;
    const components = group.componentKeys.filter((k) => panelKeys.has(k));
    if (components.length === 0) return fill(text.markerIncomplete, no);
    for (const ck of layout.colorKeys) {
      const count = markerFabricsOf(components, ck, ctx.fabrics).length;
      if (count === 0) return fill(text.markerNoFabric, no, ctx.colorName(ck));
      if (count > 1) return fill(text.markerManyFabrics, no, ctx.colorName(ck));
    }
  }
  return '';
}

/** Ở mỗi màu của dạng, chi tiết cắt có vải phải nằm đúng một nhóm của dạng; chuỗi rỗng = hợp lệ. */
function coverageProblem(layout: LayoutRow, ctx: Context): string {
  for (const ck of layout.colorKeys) {
    for (const panel of ctx.panels) {
      if (ctx.fabrics.get(panel.key)?.get(ck) === undefined) continue;
      const count = layout.groups.filter((g) => g.componentKeys.includes(panel.key)).length;
      if (count === 1) continue;
      const message = count === 0 ? text.panelWithoutMarker : text.panelManyMarkers;
      return fill(message, ctx.colorName(ck), panel.componentName.trim());
    }
  }
  return '';
}

function layoutProblem(layout: LayoutRow, index: number, ctx: Context): string {
  const name = layoutName(index);
  if (layout.colorKeys.length === 0 || layout.groups.length === 0)
    return fill(text.layoutIncomplete, name);
  return groupProblem(layout, ctx) || coverageProblem(layout, ctx);
}

/** Lỗi nhập dạng bìa / nhóm bìa (hiện cho người dùng trước khi gửi, khớp kiểm ở BE); chuỗi rỗng = hợp lệ. */
export function validateLayouts(
  layouts: LayoutRow[],
  variants: VariantRow[],
  norms: NormRow[],
  components: ComponentRow[],
  materialOptions: MaterialOption[],
): string {
  if (layouts.flatMap((l) => l.groups).length > MAX_MARKERS)
    return fill(text.maxMarkers, MAX_MARKERS);
  const names = new Map(distinctColors(variants).map((c) => [colorKey(c), c]));
  const ctx: Context = {
    panels: components.filter((c) => c.componentType === 'Panel'),
    fabrics: panelFabrics(norms, components, materialOptions),
    colorName: (ck) => names.get(ck) ?? ck,
  };
  const seen = new Set<string>();
  for (const ck of layouts.flatMap((l) => l.colorKeys)) {
    if (seen.has(ck)) return fill(text.layoutColorTwice, ctx.colorName(ck));
    seen.add(ck);
  }
  for (const [index, layout] of layouts.entries()) {
    const problem = layoutProblem(layout, index, ctx);
    if (problem) return problem;
  }
  return '';
}
