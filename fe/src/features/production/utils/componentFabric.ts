import type { ProductSpec } from '@/features/orders';
import type { OperationComponentFabric } from '../types';

/** Loại NVL vải, đúng giá trị BE trả về trong định mức. */
const MATERIAL_TYPE_FABRIC = 'FABRIC';
const COMPONENT_TYPE_PANEL = 'Panel';

/** Khóa của một cặp chi tiết × vải trong ô chọn. */
export const componentFabricKey = ({ productComponentId, materialId }: OperationComponentFabric) =>
  `${productComponentId}:${materialId}`;

export const fromComponentFabricKeys = (keys: string[]): OperationComponentFabric[] =>
  keys.map((key) => {
    const [productComponentId, materialId] = key.split(':').map(Number);
    return { productComponentId: productComponentId ?? 0, materialId: materialId ?? 0 };
  });

/**
 * Lựa chọn chi tiết × vải cho công đoạn (DEV-ALL-86): chi tiết PANEL đang dùng của mã hàng, mỗi vải có trong định mức của chi tiết đó
 * là một lựa chọn (vải theo màu nên một chi tiết có thể nhiều dòng). Phụ liệu và NVL không phải vải không gắn công đoạn (BE cũng kiểm).
 */
export function componentFabricOptions(spec: ProductSpec | undefined) {
  if (!spec) return [];
  const panels = spec.components.filter(
    (c) => c.componentType === COMPONENT_TYPE_PANEL && c.status === 'Active',
  );
  const options = new Map<string, { value: string; label: string }>();
  for (const panel of panels) {
    const name = panel.componentName.toUpperCase();
    for (const n of spec.norms) {
      if (n.materialType !== MATERIAL_TYPE_FABRIC || n.componentName.toUpperCase() !== name)
        continue;
      const value = componentFabricKey({
        productComponentId: panel.productComponentId,
        materialId: n.materialId,
      });
      const color = n.materialColor ? ` - ${n.materialColor}` : '';
      options.set(value, {
        value,
        label: `${panel.componentName} – ${n.materialCode} ${n.materialName}${color}`,
      });
    }
  }
  return [...options.values()];
}
