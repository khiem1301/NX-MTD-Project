import type {
  ProductNormItem,
  ProductSpec,
  SaveProductSpecPayload,
} from '../types';

/** Một dòng định mức trên màn hình: một NVL của một thành phần, áp dụng cho nhiều biến thể (màu × size) cùng lúc. */
export type NormRow = {
  key: string;
  componentName?: string;
  /** NVL đã gồm màu (26/10: màu nằm trong Material.Color). */
  materialId?: number;
  qtyPerGarment: number | null;
  wastagePercent: number;
  lengthCm: number | null;
  widthCm: number | null;
  specNote: string;
  variantKeys: string[];
};

export type ComponentRow = { key: string; productComponentId: number | null; componentName: string; componentType: 'Panel' | 'Trim'; piecesPerGarment: number; sortOrder: number };
export type VariantRow = { key: string; productVariantId: number | null; color: string; sizeCode: string };

export const variantKey = (color: string, sizeCode: string) =>
  `${color.trim().toUpperCase()}|${sizeCode.trim().toUpperCase()}`;

const sameRowKey = (n: ProductNormItem) =>
  [n.componentName, n.materialId, n.qtyPerGarment, n.wastagePercent, n.lengthCm ?? '', n.widthCm ?? '', n.specNote ?? ''].join('¦');

let counter = 0;
export const newKey = () => `k${++counter}`;

/**
 * Máy chủ lưu định mức theo từng biến thể; trên màn hình gộp các biến thể có cùng định mức thành một dòng
 * (đỡ nhập lặp) và chỉ giữ thành phần / biến thể còn dùng.
 */
export function toRows(spec: ProductSpec): { components: ComponentRow[]; variants: VariantRow[]; norms: NormRow[] } {
  const components = spec.components
    .filter((c) => c.status === 'Active')
    .map<ComponentRow>((c) => ({
      key: newKey(),
      productComponentId: c.productComponentId,
      componentName: c.componentName,
      componentType: c.componentType,
      piecesPerGarment: c.piecesPerGarment,
      sortOrder: c.sortOrder,
    }));
  const variants = spec.variants
    .filter((v) => v.status === 'Active')
    .map<VariantRow>((v) => ({ key: newKey(), productVariantId: v.productVariantId, color: v.color, sizeCode: v.sizeCode }));

  const activeComponents = new Set(components.map((c) => c.componentName.toUpperCase()));
  const activeVariants = new Set(variants.map((v) => variantKey(v.color, v.sizeCode)));
  const groups = new Map<string, NormRow>();
  for (const n of spec.norms) {
    if (!activeComponents.has(n.componentName.toUpperCase()) || !activeVariants.has(variantKey(n.color, n.sizeCode))) continue;
    const k = sameRowKey(n);
    const row = groups.get(k) ?? {
      key: newKey(),
      componentName: n.componentName,
      materialId: n.materialId,
      qtyPerGarment: n.qtyPerGarment,
      wastagePercent: n.wastagePercent,
      lengthCm: n.lengthCm,
      widthCm: n.widthCm,
      specNote: n.specNote ?? '',
      variantKeys: [],
    };
    row.variantKeys.push(variantKey(n.color, n.sizeCode));
    groups.set(k, row);
  }
  return { components, variants, norms: [...groups.values()] };
}

/** Lỗi nhập định mức (hiện cho người dùng trước khi gửi); chuỗi rỗng = hợp lệ. */
export function validate(components: ComponentRow[], variants: VariantRow[], norms: NormRow[]): string {
  if (components.some((c) => !c.componentName.trim())) return 'Mỗi thành phần phải có tên.';
  if (new Set(components.map((c) => c.componentName.trim().toUpperCase())).size !== components.length) return 'Tên thành phần bị trùng.';
  if (variants.some((v) => !v.color.trim() || !v.sizeCode.trim())) return 'Mỗi biến thể phải có màu và size.';
  if (new Set(variants.map((v) => variantKey(v.color, v.sizeCode))).size !== variants.length) return 'Màu – size bị trùng.';
  for (const n of norms) {
    if (!n.componentName || n.materialId === undefined) return 'Mỗi dòng định mức phải chọn thành phần và NVL.';
    if (!n.qtyPerGarment || n.qtyPerGarment <= 0) return 'Định mức trên một sản phẩm phải lớn hơn 0.';
    if (n.variantKeys.length === 0) return 'Mỗi dòng định mức phải áp dụng cho ít nhất một màu – size.';
  }
  const seen = new Set<string>();
  for (const n of norms) {
    for (const vk of n.variantKeys) {
      const k = `${n.componentName!.toUpperCase()}¦${n.materialId}¦${vk}`;
      if (seen.has(k)) return 'Một NVL chỉ khai một dòng cho mỗi thành phần và màu – size.';
      seen.add(k);
    }
  }
  return '';
}

/** Nở mỗi dòng định mức ra từng biến thể (máy chủ lưu theo biến thể × thành phần × NVL). */
export function toPayload(components: ComponentRow[], variants: VariantRow[], norms: NormRow[]): SaveProductSpecPayload {
  const byKey = new Map(variants.map((v) => [variantKey(v.color, v.sizeCode), v]));
  return {
    components: components.map((c) => ({
      productComponentId: c.productComponentId,
      componentName: c.componentName.trim(),
      componentType: c.componentType,
      piecesPerGarment: c.piecesPerGarment,
      sortOrder: c.sortOrder,
    })),
    variants: variants.map((v) => ({ productVariantId: v.productVariantId, color: v.color.trim(), sizeCode: v.sizeCode.trim() })),
    norms: norms.flatMap((n) =>
      n.variantKeys
        .filter((vk) => byKey.has(vk))
        .map((vk) => {
          const v = byKey.get(vk)!;
          return {
            componentName: n.componentName!.trim(),
            color: v.color.trim(),
            sizeCode: v.sizeCode.trim(),
            materialId: n.materialId!,
            qtyPerGarment: n.qtyPerGarment!,
            wastagePercent: n.wastagePercent,
            lengthCm: n.lengthCm,
            widthCm: n.widthCm,
            specNote: n.specNote.trim() || null,
          };
        }),
    ),
  };
}
