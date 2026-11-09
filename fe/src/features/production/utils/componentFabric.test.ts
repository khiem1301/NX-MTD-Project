import type { ProductSpec } from '@/features/orders';
import {
  componentFabricKey,
  componentFabricOptions,
  fromComponentFabricKeys,
} from './componentFabric';

const norm = (componentName: string, materialId: number, materialType: string, color = '') => ({
  productComponentMaterialId: materialId,
  componentName,
  color: 'Đen',
  sizeCode: 'M',
  materialId,
  materialCode: `V${materialId}`,
  materialName: 'Vải',
  unit: 'm',
  materialColor: color,
  specNote: null,
  materialType,
});

const component = (id: number, name: string, type: 'Panel' | 'Trim', status = 'Active') => ({
  productComponentId: id,
  componentName: name,
  componentType: type,
  piecesPerGarment: 1,
  status,
});

const spec = {
  productId: 1,
  components: [
    component(1, 'Thân', 'Panel'),
    component(2, 'Nhãn', 'Trim'),
    component(3, 'Cổ cũ', 'Panel', 'Discontinued'),
  ],
  variants: [],
  norms: [
    norm('Thân', 10, 'FABRIC', 'Đen'),
    norm('thân', 10, 'FABRIC', 'Đen'),
    norm('Thân', 11, 'FABRIC'),
    norm('Thân', 12, 'TRIM'),
    norm('Nhãn', 13, 'FABRIC'),
    norm('Cổ cũ', 10, 'FABRIC'),
  ],
  markerComponents: [],
  markerFabrics: [],
} as ProductSpec;

describe('componentFabricOptions', () => {
  it('mỗi vải của chi tiết cắt đang dùng là một lựa chọn, bỏ trùng; không có phụ liệu, NVL không phải vải', () => {
    expect(componentFabricOptions(spec)).toEqual([
      { value: '1:10', label: 'Thân – V10 Vải - Đen' },
      { value: '1:11', label: 'Thân – V11 Vải' },
    ]);
  });

  it('chưa tải định mức thì rỗng', () => {
    expect(componentFabricOptions(undefined)).toEqual([]);
  });
});

describe('componentFabricKey', () => {
  it('đổi qua lại giữa cặp chi tiết × vải và khóa', () => {
    const pair = { productComponentId: 1, materialId: 10 };

    expect(fromComponentFabricKeys([componentFabricKey(pair)])).toEqual([pair]);
  });
});
