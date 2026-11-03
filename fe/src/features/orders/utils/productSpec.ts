import { foldVietnamese } from '@/utils/colorSearch';
import { MESSAGES } from '@/constants/messages';
import { fill, formatQty } from '@/utils/format';
import type { ProductNormItem, ProductSpec, SaveProductSpecPayload } from '../types';

const text = MESSAGES.orders.specValidation;

/** Giới hạn khớp SaveProductSpecDtoValidator ở BE (cột DECIMAL(7,2), SMALLINT và số dòng mỗi lần lưu). */
export const MAX_SIZE_CM = 99999.99;
export const MAX_PIECES_PER_GARMENT = 99;
export const MAX_COMPONENTS = 200;
export const MAX_VARIANTS = 200;
/** Tính sau khi mỗi dòng định mức được nở ra từng màu – size. */
export const MAX_NORMS = 5000;

/** Mã loại NVL vải, đúng giá trị API lookup trả về. */
const MATERIAL_TYPE_FABRIC = 'FABRIC';

/** Kích thước bỏ trống được; đã nhập thì phải lớn hơn 0 và không vượt giới hạn cột (vượt thì database báo tràn số). */
const sizeProblem = (name: string, label: string, value: number | null): string =>
  value !== null && (value <= 0 || value > MAX_SIZE_CM) ? fill(text.sizeRange, name, label) : '';

/**
 * Một dòng định mức trên màn hình: một NVL của một thành phần, áp dụng cho nhiều biến thể (màu × size) cùng lúc.
 * Thành phần được trỏ bằng khóa dòng (`ComponentRow.key`) chứ không bằng tên, nên đổi tên thành phần thì dòng định mức đi theo.
 */
export type NormRow = {
  key: string;
  componentKey?: string;
  /** NVL đã gồm màu (26/10: màu nằm trong Material.Color). */
  materialId?: number;
  lengthCm: number | null;
  widthCm: number | null;
  specNote: string;
  variantKeys: string[];
};

export type ComponentRow = {
  key: string;
  productComponentId: number | null;
  componentName: string;
  componentType: 'Panel' | 'Trim';
  piecesPerGarment: number;
};
/** Màu – size do đơn hàng tạo (chỉ đọc). */
export type VariantRow = {
  key: string;
  productVariantId: number | null;
  color: string;
  sizeCode: string;
};

export const variantKey = (color: string, sizeCode: string) =>
  `${color.trim().toUpperCase()}|${sizeCode.trim().toUpperCase()}`;

const sameRowKey = (n: ProductNormItem) =>
  [n.componentName, n.materialId, n.lengthCm ?? '', n.widthCm ?? '', n.specNote ?? ''].join('¦');

let counter = 0;
export const newKey = () => `k${++counter}`;

/**
 * Máy chủ lưu định mức theo từng biến thể; trên màn hình gộp các biến thể có cùng định mức thành một dòng
 * (đỡ nhập lặp) và chỉ giữ thành phần / biến thể còn dùng.
 */
export function toRows(spec: ProductSpec): {
  components: ComponentRow[];
  variants: VariantRow[];
  norms: NormRow[];
} {
  const components = spec.components
    .filter((c) => c.status === 'Active')
    .map<ComponentRow>((c) => ({
      key: newKey(),
      productComponentId: c.productComponentId,
      componentName: c.componentName,
      componentType: c.componentType,
      piecesPerGarment: c.piecesPerGarment,
    }));
  const variants = spec.variants
    .filter((v) => v.status === 'Active')
    .map<VariantRow>((v) => ({
      key: newKey(),
      productVariantId: v.productVariantId,
      color: v.color,
      sizeCode: v.sizeCode,
    }));

  const keyByName = new Map(components.map((c) => [c.componentName.toUpperCase(), c.key]));
  const activeVariants = new Set(variants.map((v) => variantKey(v.color, v.sizeCode)));
  const groups = new Map<string, NormRow>();
  for (const n of spec.norms) {
    const componentKey = keyByName.get(n.componentName.toUpperCase());
    if (componentKey === undefined || !activeVariants.has(variantKey(n.color, n.sizeCode)))
      continue;
    const k = sameRowKey(n);
    const row = groups.get(k) ?? {
      key: newKey(),
      componentKey,
      materialId: n.materialId,
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

/** `label`: đủ mã, tên, màu, đơn vị (danh sách chọn và tìm kiếm); `short`: chỉ mã - màu (ô đã chọn trong bảng hẹp). */
export type MaterialOption = { value: number; label: string; short: string; isFabric: boolean };

export const toMaterialOption = (m: {
  materialId: number;
  materialCode: string;
  materialName: string;
  color: string;
  unit: string;
  materialType?: string;
}): MaterialOption => ({
  value: m.materialId,
  label: `${m.materialCode} - ${m.materialName}${m.color ? ` - ${m.color}` : ''} (${m.unit})`,
  short: `${m.materialCode}${m.color ? ` - ${m.color}` : ''}`,
  isFabric: m.materialType === MATERIAL_TYPE_FABRIC,
});

/** Số dòng định mức máy chủ sẽ nhận: mỗi dòng nở ra từng màu – size còn tồn tại (xem `toPayload`). */
const expandedNormCount = (variants: VariantRow[], norms: NormRow[]): number => {
  const known = new Set(variants.map((v) => variantKey(v.color, v.sizeCode)));
  return norms.reduce((sum, n) => sum + n.variantKeys.filter((vk) => known.has(vk)).length, 0);
};

/** Lỗi nhập định mức (hiện cho người dùng trước khi gửi); chuỗi rỗng = hợp lệ. */
export function validate(
  components: ComponentRow[],
  variants: VariantRow[],
  norms: NormRow[],
): string {
  if (components.length > MAX_COMPONENTS) return fill(text.maxComponents, MAX_COMPONENTS);
  if (variants.length > MAX_VARIANTS) return fill(text.maxVariants, MAX_VARIANTS);
  if (components.some((c) => !c.componentName.trim())) return 'Mỗi thành phần phải có tên.';
  if (
    new Set(components.map((c) => c.componentName.trim().toUpperCase())).size !== components.length
  )
    return 'Tên thành phần bị trùng.';
  if (variants.some((v) => !v.color.trim() || !v.sizeCode.trim()))
    return 'Mỗi biến thể phải có màu và size.';
  if (new Set(variants.map((v) => variantKey(v.color, v.sizeCode))).size !== variants.length)
    return 'Màu – size bị trùng.';
  const badPieces = components.find(
    (c) => c.piecesPerGarment < 1 || c.piecesPerGarment > MAX_PIECES_PER_GARMENT,
  );
  if (badPieces) return fill(text.piecesRange, badPieces.componentName.trim());
  const nameByKey = new Map(components.map((c) => [c.key, c.componentName.trim()]));
  for (const n of norms) {
    const name = n.componentKey === undefined ? undefined : nameByKey.get(n.componentKey);
    if (name === undefined || n.materialId === undefined)
      return 'Mỗi dòng định mức phải chọn thành phần và NVL.';
    const problem =
      sizeProblem(name, text.normLength, n.lengthCm) ||
      sizeProblem(name, text.normWidth, n.widthCm);
    if (problem) return problem;
    if (n.variantKeys.length === 0)
      return 'Mỗi dòng định mức phải áp dụng cho ít nhất một màu – size.';
  }
  const seen = new Set<string>();
  for (const n of norms) {
    for (const vk of n.variantKeys) {
      const k = `${n.componentKey}¦${n.materialId}¦${vk}`;
      if (seen.has(k)) return 'Một NVL chỉ khai một dòng cho mỗi thành phần và màu – size.';
      seen.add(k);
    }
  }
  const total = expandedNormCount(variants, norms);
  if (total > MAX_NORMS) return fill(text.maxNorms, formatQty(total), formatQty(MAX_NORMS));
  return '';
}

/** Nở mỗi dòng định mức ra từng biến thể (máy chủ lưu theo biến thể × thành phần × NVL). */
export function toPayload(
  components: ComponentRow[],
  variants: VariantRow[],
  norms: NormRow[],
): Omit<SaveProductSpecPayload, 'markers' | 'markerFabrics'> {
  const byKey = new Map(variants.map((v) => [variantKey(v.color, v.sizeCode), v]));
  const nameByKey = new Map(components.map((c) => [c.key, c.componentName.trim()]));
  return {
    components: components.map((c) => ({
      productComponentId: c.productComponentId,
      componentName: c.componentName.trim(),
      componentType: c.componentType,
      piecesPerGarment: c.piecesPerGarment,
    })),
    variants: variants.map((v) => ({
      productVariantId: v.productVariantId,
      color: v.color.trim(),
      sizeCode: v.sizeCode.trim(),
    })),
    norms: norms.flatMap((n) =>
      n.variantKeys
        .filter((vk) => byKey.has(vk))
        .map((vk) => {
          const v = byKey.get(vk)!;
          return {
            componentName: nameByKey.get(n.componentKey!)!,
            color: v.color.trim(),
            sizeCode: v.sizeCode.trim(),
            materialId: n.materialId!,
            lengthCm: n.lengthCm,
            widthCm: n.widthCm,
            specNote: n.specNote.trim() || null,
          };
        }),
    ),
  };
}

/** Xóa một thành phần kèm các dòng định mức dùng nó (không để dòng định mức trỏ vào thành phần không còn). */
export function removeComponent(
  components: ComponentRow[],
  norms: NormRow[],
  key: string,
): { components: ComponentRow[]; norms: NormRow[]; removedNorms: number } {
  return {
    components: components.filter((c) => c.key !== key),
    norms: norms.filter((n) => n.componentKey !== key),
    removedNorms: norms.filter((n) => n.componentKey === key).length,
  };
}

/** Sửa một dòng của bảng nhập liệu theo khóa dòng (dùng chung cho thành phần, biến thể, định mức). */
export const patchRow = <T extends { key: string }>(
  set: (updater: (rows: T[]) => T[]) => void,
  key: string,
  change: Partial<T>,
) => set((rows) => rows.map((r) => (r.key === key ? { ...r, ...change } : r)));

/** Bỏ một dòng của bảng nhập liệu theo khóa dòng. */
export const dropRow = <T extends { key: string }>(
  set: (updater: (rows: T[]) => T[]) => void,
  key: string,
) => set((rows) => rows.filter((r) => r.key !== key));

export type NormFilter = {
  keyword?: string;
  component?: string;
  materialId?: number;
  color?: string;
  size?: string;
};

/** Lọc bảng định mức; searchText cho biết chữ dùng để tìm từ khóa trên từng dòng. */
export const filterNorms = (
  norms: NormRow[],
  filter: NormFilter,
  variants: VariantRow[],
  searchText: (row: NormRow) => string,
): NormRow[] => {
  const appliesTo = (r: NormRow, match: (v: VariantRow) => boolean) =>
    r.variantKeys.some((k) =>
      variants.some((v) => variantKey(v.color, v.sizeCode) === k && match(v)),
    );
  const keyword = foldVietnamese(filter.keyword ?? '');
  return norms.filter((r) => {
    if (filter.component && r.componentKey !== filter.component) return false;
    if (filter.materialId !== undefined && r.materialId !== filter.materialId) return false;
    if (filter.color && !appliesTo(r, (v) => v.color.trim() === filter.color)) return false;
    if (filter.size && !appliesTo(r, (v) => v.sizeCode.trim() === filter.size)) return false;
    return !keyword || foldVietnamese(searchText(r)).includes(keyword);
  });
};
