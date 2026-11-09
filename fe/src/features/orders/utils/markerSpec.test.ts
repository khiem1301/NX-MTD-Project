import type { ProductSpec } from '../types';
import {
  codeKey,
  distinctColors,
  emptyMarker,
  fabricOptionsFor,
  toMarkerPayload,
  toMarkerRows,
  validateMarkers,
  type MarkerRow,
} from './markerSpec';
import {
  toMaterialOption,
  variantKey,
  type ComponentRow,
  type NormRow,
  type VariantRow,
} from './productSpec';

const variant = (color: string, sizeCode: string): VariantRow => ({
  key: `${color}-${sizeCode}`,
  productVariantId: 1,
  color,
  sizeCode,
});
const variants = [variant('Đen', 'M'), variant('Đen', 'L'), variant('Đỏ', 'M')];

const component = (key: string, componentType: 'Panel' | 'Trim'): ComponentRow => ({
  key,
  productComponentId: 1,
  componentName: key,
  componentType,
  piecesPerGarment: 1,
});
const components = [
  component('than', 'Panel'),
  component('tay', 'Panel'),
  component('khoa', 'Trim'),
];

const norm = (componentKey: string, materialId: number, ...keys: string[]): NormRow => ({
  key: `${componentKey}-${materialId}`,
  componentKey,
  materialId,
  specNote: '',
  variantKeys: keys,
});
const norms = [
  norm('than', 1, variantKey('Đen', 'M'), variantKey('Đen', 'L')),
  norm('than', 2, variantKey('Đỏ', 'M')),
  norm('khoa', 3, variantKey('Đen', 'M')),
];

const material = (materialId: number, materialType: string) =>
  toMaterialOption({
    materialId,
    materialCode: `V${materialId}`,
    materialName: 'Vải',
    color: '',
    unit: 'm',
    materialType,
  });
const materials = [material(1, 'FABRIC'), material(2, 'FABRIC'), material(3, 'TRIM')];

const marker = (change: Partial<MarkerRow> = {}): MarkerRow => ({
  key: 'b1',
  markerNo: 1,
  componentKeys: ['than', 'tay'],
  fabrics: { [codeKey('Đen')]: 1 },
  ...change,
});
const validate = (rows: MarkerRow[]) =>
  validateMarkers(rows, variants, norms, components, materials);

describe('fabricOptionsFor', () => {
  it('chỉ gồm vải của chi tiết cắt thuộc đúng màu áo', () => {
    expect(fabricOptionsFor('Đen', norms, components, materials).map((o) => o.value)).toEqual([1]);
    expect(fabricOptionsFor('Đỏ', norms, components, materials).map((o) => o.value)).toEqual([2]);
    expect(fabricOptionsFor('Xanh', norms, components, materials)).toEqual([]);
  });
});

describe('distinctColors', () => {
  it('bỏ trùng theo màu', () => {
    expect(distinctColors(variants)).toEqual(['Đen', 'Đỏ']);
  });
});

describe('emptyMarker', () => {
  it('lấy số nhóm nhỏ nhất chưa dùng', () => {
    expect(emptyMarker([]).markerNo).toBe(1);
    expect(emptyMarker([marker({ markerNo: 1 }), marker({ markerNo: 3 })]).markerNo).toBe(2);
  });
});

describe('toMarkerRows', () => {
  const spec = {
    markerComponents: [
      { markerNo: 2, componentName: 'TAY' },
      { markerNo: 1, componentName: 'than' },
      { markerNo: 1, componentName: 'Cổ cũ' },
    ],
    markerFabrics: [
      {
        markerNo: 1,
        color: 'Đen',
        materialId: 1,
        materialCode: 'V1',
        materialName: 'Vải',
        materialColor: '',
      },
      {
        markerNo: 2,
        color: 'Tím',
        materialId: 2,
        materialCode: 'V2',
        materialName: 'Vải',
        materialColor: '',
      },
    ],
  } as unknown as ProductSpec;

  it('gom theo số nhóm, giữ số nhóm và bỏ chi tiết, màu không còn dùng', () => {
    const rows = toMarkerRows(spec, variants, components);

    expect(rows.map((r) => r.markerNo)).toEqual([1, 2]);
    expect(rows[0]?.componentKeys).toEqual(['than']);
    expect(rows[0]?.fabrics).toEqual({ [codeKey('Đen')]: 1 });
    expect(rows[1]?.componentKeys).toEqual(['tay']);
    expect(rows[1]?.fabrics).toEqual({});
  });
});

describe('validateMarkers', () => {
  it('chấp nhận nhóm đủ chi tiết và vải, hoặc không có nhóm nào', () => {
    expect(validate([marker()])).toBe('');
    expect(validate([])).toBe('');
  });

  it('báo nhóm chưa có chi tiết hoặc chưa có vải', () => {
    expect(validate([marker({ componentKeys: [] })])).toContain('Nhóm bìa 1');
    expect(validate([marker({ fabrics: {} })])).toContain('Nhóm bìa 1');
  });

  it('báo chi tiết cắt chưa nằm trong nhóm nào', () => {
    expect(validate([marker({ componentKeys: ['than'] })])).toContain('"tay"');
  });

  it('báo vải không còn trong định mức của màu, kèm đúng số nhóm', () => {
    const wrong = marker({ key: 'b2', markerNo: 4, fabrics: { [codeKey('Đen')]: 2 } });

    expect(validate([marker(), wrong])).toContain('Nhóm bìa 4');
    expect(validate([marker(), wrong])).toContain('Đen');
  });

  it('giới hạn số nhóm', () => {
    const rows = Array.from({ length: 21 }, (_, i) => marker({ key: `b${i}`, markerNo: i + 1 }));

    expect(validate(rows)).toContain('20');
    expect(validate(rows.slice(0, 20))).toBe('');
  });
});

describe('toMarkerPayload', () => {
  it('nở từng nhóm ra chi tiết và màu, giữ số nhóm', () => {
    const rows = [
      marker({ componentKeys: ['than'] }),
      marker({ key: 'b2', markerNo: 3, componentKeys: ['tay'] }),
    ];

    expect(toMarkerPayload(rows, variants, components)).toEqual({
      markerComponents: [
        { markerNo: 1, componentName: 'than' },
        { markerNo: 3, componentName: 'tay' },
      ],
      markerFabrics: [
        { markerNo: 1, color: 'Đen', materialId: 1 },
        { markerNo: 3, color: 'Đen', materialId: 1 },
      ],
    });
  });

  it('bỏ màu đã bỏ chọn vải và chi tiết đã xóa', () => {
    const payload = toMarkerPayload(
      [marker({ componentKeys: ['than', 'da-xoa'], fabrics: { [codeKey('Đen')]: undefined } })],
      variants,
      components,
    );

    expect(payload.markerFabrics).toEqual([]);
    expect(payload.markerComponents).toEqual([{ markerNo: 1, componentName: 'than' }]);
  });
});
