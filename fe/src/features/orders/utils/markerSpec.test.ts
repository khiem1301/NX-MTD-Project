import type { ProductSpec } from '../types';
import {
  componentBlock,
  distinctColors,
  emptyGroup,
  emptyLayout,
  markerFabricsOf,
  panelFabrics,
  fitsLayout,
  suggestLayouts,
  toLayoutRows,
  toMarkerPayload,
  type LayoutRow,
} from './markerSpec';
import { validateLayouts } from './markerValidation';
import {
  colorKey,
  toMaterialOption,
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
const variants = [variant('Xanh', 'M'), variant('Trắng', 'M'), variant('Đen', 'M')];
const [XANH, TRANG, DEN] = ['Xanh', 'Trắng', 'Đen'].map(colorKey) as [string, string, string];

const component = (key: string, componentType: 'Panel' | 'Trim' = 'Panel'): ComponentRow => ({
  key,
  productComponentId: 1,
  componentName: key,
  componentType,
  piecesPerGarment: 1,
});
const components = [
  component('truoc'),
  component('sau'),
  component('tay'),
  component('khoa', 'Trim'),
];

const norm = (componentKey: string, materialId: number, ...keys: string[]): NormRow => ({
  key: `${componentKey}-${materialId}-${keys.join()}`,
  componentKey,
  materialId,
  specNote: '',
  colorKeys: keys,
});
/**
 * Xanh: thân trước + thân sau vải 1, tay vải 2. Trắng: cùng cách chia (thân vải 3, tay vải 4).
 * Đen: thân trước, tay vải 5 nhưng thân sau vải 6 (khác cách chia).
 */
const norms = [
  norm('truoc', 1, XANH),
  norm('sau', 1, XANH),
  norm('tay', 2, XANH),
  norm('truoc', 3, TRANG),
  norm('sau', 3, TRANG),
  norm('tay', 4, TRANG),
  norm('truoc', 5, DEN),
  norm('sau', 6, DEN),
  norm('tay', 5, DEN),
  norm('khoa', 9, DEN),
];

const material = (materialId: number, materialType = 'FABRIC') =>
  toMaterialOption({
    materialId,
    materialCode: `V${materialId}`,
    materialName: 'Vải',
    color: '',
    unit: 'm',
    materialType,
  });
const materials = [1, 2, 3, 4, 5, 6].map((id) => material(id)).concat(material(9, 'TRIM'));
const fabrics = panelFabrics(norms, components, materials);

const group = (markerNo: number, ...componentKeys: string[]) => ({
  key: `g${markerNo}`,
  markerNo,
  componentKeys,
});
/** Dạng A: Xanh, Trắng (thân chung bìa, tay riêng). Dạng B: Đen (mỗi chi tiết một bìa). */
const layoutA: LayoutRow = {
  key: 'a',
  colorKeys: [XANH, TRANG],
  groups: [group(1, 'truoc', 'sau'), group(2, 'tay')],
};
const layoutB: LayoutRow = {
  key: 'b',
  colorKeys: [DEN],
  groups: [group(3, 'truoc', 'tay'), group(4, 'sau')],
};
const validate = (layouts: LayoutRow[]) =>
  validateLayouts(layouts, variants, norms, components, materials);

describe('panelFabrics, markerFabricsOf', () => {
  it('lấy vải của chi tiết cắt theo màu, bỏ phụ liệu', () => {
    expect([...(fabrics.get('sau') ?? [])]).toEqual([
      [XANH, 1],
      [TRANG, 3],
      [DEN, 6],
    ]);
    expect(fabrics.has('khoa')).toBe(false);
    expect(markerFabricsOf(['truoc', 'sau'], DEN, fabrics)).toEqual([5, 6]);
  });
});

describe('fitsLayout', () => {
  it('màu dùng được dạng khi mỗi nhóm một vải ở màu đó, dù cách chia vải khác các màu đã có', () => {
    const splitBody: LayoutRow = {
      key: 's',
      colorKeys: [XANH],
      groups: [group(1, 'truoc'), group(2, 'sau'), group(3, 'tay')],
    };

    expect(fitsLayout(layoutA, TRANG, fabrics)).toBe(true);
    expect(fitsLayout(splitBody, DEN, fabrics)).toBe(true);
  });

  it('màu có nhóm mà chi tiết khác vải thì không dùng được dạng', () => {
    expect(fitsLayout(layoutA, DEN, fabrics)).toBe(false);
  });
});

describe('suggestLayouts', () => {
  it('màu dùng được nhóm của dạng đã có thì vào dạng đó, không thì dạng mới gồm các chi tiết cùng vải', () => {
    const layouts = suggestLayouts(variants, fabrics);

    expect(layouts.map((l) => l.colorKeys)).toEqual([[XANH, TRANG], [DEN]]);
    expect(layouts.map((l) => l.groups.map((g) => [g.markerNo, g.componentKeys]))).toEqual([
      [
        [1, ['sau', 'truoc']],
        [2, ['tay']],
      ],
      [
        [3, ['sau']],
        [4, ['tay', 'truoc']],
      ],
    ]);
    expect(validate(layouts)).toBe('');
  });
});

describe('emptyLayout, emptyGroup, distinctColors', () => {
  it('dạng mới lấy màu chưa thuộc dạng nào; nhóm mới lấy số nhỏ nhất chưa dùng ở mọi dạng', () => {
    expect(distinctColors(variants)).toEqual(['Xanh', 'Trắng', 'Đen']);
    expect(emptyLayout([layoutA], variants).colorKeys).toEqual([DEN]);
    expect(emptyGroup([layoutA, { ...layoutB, groups: [group(4, 'sau')] }]).markerNo).toBe(3);
  });
});

describe('componentBlock', () => {
  it('khóa chi tiết đã ở nhóm khác của dạng, hoặc khác vải với chi tiết đã chọn ở một màu của dạng', () => {
    const den: LayoutRow = { key: 'b', colorKeys: [DEN], groups: [group(3, 'truoc'), group(4)] };

    expect(componentBlock(layoutA, group(2, 'tay'), 'truoc', fabrics)).toEqual({ otherGroup: 1 });
    expect(componentBlock(den, group(3, 'truoc'), 'sau', fabrics)).toEqual({ colorKey: DEN });
    expect(componentBlock(den, group(3, 'truoc'), 'tay', fabrics)).toBeUndefined();
  });
});

describe('toLayoutRows', () => {
  const spec = {
    markerComponents: [
      { markerNo: 3, componentName: 'TRUOC' },
      { markerNo: 1, componentName: 'truoc' },
      { markerNo: 1, componentName: 'Cổ cũ' },
      { markerNo: 2, componentName: 'tay' },
    ],
    markerFabrics: [
      { markerNo: 1, color: 'Xanh' },
      { markerNo: 1, color: 'Trắng' },
      { markerNo: 2, color: 'Trắng' },
      { markerNo: 2, color: 'Xanh' },
      { markerNo: 3, color: 'Đen' },
      { markerNo: 3, color: 'Tím' },
    ],
  } as unknown as ProductSpec;

  it('gom các nhóm có cùng tập màu thành một dạng bìa, bỏ chi tiết và màu không còn dùng', () => {
    const layouts = toLayoutRows(spec, variants, components);

    expect(
      layouts.map((l) => [l.colorKeys, l.groups.map((g) => [g.markerNo, g.componentKeys])]),
    ).toEqual([
      [
        [XANH, TRANG],
        [
          [1, ['truoc']],
          [2, ['tay']],
        ],
      ],
      [[DEN], [[3, ['truoc']]]],
    ]);
  });
});

describe('validateLayouts', () => {
  it('chấp nhận mỗi dạng một cách chia, hoặc không có dạng nào', () => {
    expect(validate([layoutA, layoutB])).toBe('');
    expect(validate([])).toBe('');
  });

  it('báo dạng chưa có màu hoặc chưa có nhóm, và màu nằm ở hai dạng', () => {
    expect(validate([{ ...layoutA, colorKeys: [] }])).toContain('Dạng bìa A');
    expect(validate([{ ...layoutA, groups: [] }])).toContain('Dạng bìa A');
    expect(validate([layoutA, { ...layoutB, colorKeys: [DEN, XANH] }])).toContain(
      'Màu Xanh nằm ở hơn một dạng bìa',
    );
  });

  it('báo màu có nhóm mà chi tiết khác vải, dù các màu khác của dạng dùng được', () => {
    expect(validate([{ ...layoutA, colorKeys: [XANH, TRANG, DEN] }])).toContain(
      'Nhóm bìa 1, màu Đen',
    );
  });

  it('báo nhóm có chi tiết khác vải, chi tiết chưa nằm hoặc nằm hai nhóm của dạng', () => {
    expect(
      validate([{ ...layoutB, groups: [group(3, 'truoc', 'sau'), group(4, 'tay')] }]),
    ).toContain('Nhóm bìa 3, màu Đen');
    expect(validate([{ ...layoutB, groups: [group(3, 'truoc', 'tay')] }])).toContain(
      '"sau" chưa nằm',
    );
    expect(validate([{ ...layoutB, groups: [...layoutB.groups, group(5, 'sau')] }])).toContain(
      'hơn một nhóm',
    );
  });
});

describe('toMarkerPayload', () => {
  it('nở từng nhóm ra chi tiết và các màu của dạng (tên gốc), bỏ phụ liệu và chi tiết đã xóa', () => {
    const payload = toMarkerPayload(
      [layoutA, { ...layoutB, groups: [group(3, 'truoc', 'khoa', 'da-xoa')] }],
      variants,
      components,
    );

    expect(payload.markerComponents).toEqual([
      { markerNo: 1, componentName: 'truoc' },
      { markerNo: 1, componentName: 'sau' },
      { markerNo: 2, componentName: 'tay' },
      { markerNo: 3, componentName: 'truoc' },
    ]);
    expect(payload.markerColors).toEqual([
      { markerNo: 1, color: 'Xanh' },
      { markerNo: 1, color: 'Trắng' },
      { markerNo: 2, color: 'Xanh' },
      { markerNo: 2, color: 'Trắng' },
      { markerNo: 3, color: 'Đen' },
    ]);
  });
});
