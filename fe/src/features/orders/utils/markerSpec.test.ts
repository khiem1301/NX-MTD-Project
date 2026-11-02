import type { ProductSpec } from '../types';
import {
  codeKey,
  distinctColors,
  distinctSizes,
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
const components = [component('than', 'Panel'), component('khoa', 'Trim')];

const norm = (componentKey: string, materialId: number, ...keys: string[]): NormRow => ({
  key: `${componentKey}-${materialId}`,
  componentKey,
  materialId,
  lengthCm: null,
  widthCm: null,
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
  sizes: { M: { lengthCm: 120, widthCm: 70 } },
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

describe('distinctSizes, distinctColors', () => {
  it('bỏ trùng theo màu – size', () => {
    expect(distinctSizes(variants)).toEqual(['M', 'L']);
    expect(distinctColors(variants)).toEqual(['Đen', 'Đỏ']);
  });
});

describe('toMarkerRows', () => {
  const spec = {
    markers: [
      { markerNo: 2, sizeCode: 'M', lengthCm: 40, widthCm: 30 },
      { markerNo: 1, sizeCode: 'M', lengthCm: 120, widthCm: 70 },
      { markerNo: 1, sizeCode: 'XXL', lengthCm: 130, widthCm: 80 },
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

  it('gom theo số bìa và bỏ size, màu không còn dùng', () => {
    const rows = toMarkerRows(spec, variants);

    expect(rows).toHaveLength(2);
    expect(rows[0]?.sizes).toEqual({ M: { lengthCm: 120, widthCm: 70 } });
    expect(rows[0]?.fabrics).toEqual({ [codeKey('Đen')]: 1 });
    expect(rows[1]?.sizes).toEqual({ M: { lengthCm: 40, widthCm: 30 } });
    expect(rows[1]?.fabrics).toEqual({});
  });
});

describe('validateMarkers', () => {
  it('chấp nhận bìa đủ kích thước và vải, hoặc không có bìa nào', () => {
    expect(validate([marker()])).toBe('');
    expect(validate([])).toBe('');
  });

  it('báo thiếu một trong hai kích thước của một size', () => {
    expect(validate([marker({ sizes: { M: { lengthCm: 120, widthCm: null } } })])).toContain(
      'Bìa 1',
    );
  });

  it('báo kích thước ngoài giới hạn', () => {
    expect(validate([marker({ sizes: { M: { lengthCm: 0, widthCm: 70 } } })])).not.toBe('');
    expect(validate([marker({ sizes: { M: { lengthCm: 100000, widthCm: 70 } } })])).not.toBe('');
    expect(validate([marker({ sizes: { M: { lengthCm: 99999.99, widthCm: 70 } } })])).toBe('');
  });

  it('báo bìa chưa có kích thước hoặc chưa có vải', () => {
    expect(validate([marker({ sizes: {} })])).not.toBe('');
    expect(validate([marker({ fabrics: {} })])).not.toBe('');
  });

  it('báo vải không còn trong định mức của màu, và đúng số bìa', () => {
    const wrong = marker({ key: 'b2', fabrics: { [codeKey('Đen')]: 2 } });

    expect(validate([marker(), wrong])).toContain('Bìa 2');
    expect(validate([marker(), wrong])).toContain('Đen');
  });

  it('giới hạn số bìa', () => {
    const rows = Array.from({ length: 21 }, (_, i) => marker({ key: `b${i}` }));

    expect(validate(rows)).toContain('20');
    expect(validate(rows.slice(0, 20))).toBe('');
  });
});

describe('toMarkerPayload', () => {
  it('nở từng bìa ra size và màu, số bìa theo vị trí', () => {
    const rows = [
      marker({ key: 'b1' }),
      marker({
        key: 'b2',
        sizes: { M: { lengthCm: 40, widthCm: 30 }, L: { lengthCm: null, widthCm: null } },
      }),
    ];

    expect(toMarkerPayload(rows, variants)).toEqual({
      markers: [
        { markerNo: 1, sizeCode: 'M', lengthCm: 120, widthCm: 70 },
        { markerNo: 2, sizeCode: 'M', lengthCm: 40, widthCm: 30 },
      ],
      markerFabrics: [
        { markerNo: 1, color: 'Đen', materialId: 1 },
        { markerNo: 2, color: 'Đen', materialId: 1 },
      ],
    });
  });

  it('không gửi dòng màu đã bỏ chọn vải', () => {
    const payload = toMarkerPayload(
      [marker({ fabrics: { [codeKey('Đen')]: undefined } })],
      variants,
    );

    expect(payload.markerFabrics).toEqual([]);
  });
});
