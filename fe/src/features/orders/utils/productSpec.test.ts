import type { ProductSpec } from '../types';
import {
  removeComponent,
  toPayload,
  toRows,
  validate,
  variantKey,
  type ComponentRow,
  type NormRow,
  type VariantRow,
} from './productSpec';

const norm = (
  id: number,
  component: string,
  color: string,
  size: string,
  length: number | null = null,
  material = 1,
) => ({
  productComponentMaterialId: id,
  componentName: component,
  color,
  sizeCode: size,
  materialId: material,
  materialCode: 'V1',
  materialName: 'Vải',
  unit: 'm',
  materialColor: 'Đen',
  lengthCm: length,
  widthCm: null,
  specNote: null,
});

const spec: ProductSpec = {
  productId: 1,
  components: [
    {
      productComponentId: 1,
      componentName: 'Thân',
      componentType: 'Panel',
      piecesPerGarment: 1,
      status: 'Active',
    },
    {
      productComponentId: 2,
      componentName: 'Cổ cũ',
      componentType: 'Panel',
      piecesPerGarment: 1,
      status: 'Discontinued',
    },
  ],
  variants: [
    {
      productVariantId: 10,
      color: 'Đen',
      sizeCode: 'M',
      status: 'Active',
    },
    {
      productVariantId: 11,
      color: 'Đen',
      sizeCode: 'L',
      status: 'Active',
    },
    {
      productVariantId: 12,
      color: 'Đỏ',
      sizeCode: 'S',
      status: 'Discontinued',
    },
  ],
  norms: [
    norm(1, 'Thân', 'Đen', 'M', 60),
    norm(2, 'Thân', 'Đen', 'L', 60),
    norm(3, 'Cổ cũ', 'Đen', 'M'),
    norm(4, 'Thân', 'Đỏ', 'S'),
  ],
};

describe('productSpec', () => {
  it('gộp các biến thể cùng định mức thành một dòng và bỏ thành phần / biến thể đã ngừng dùng', () => {
    const rows = toRows(spec);

    expect(rows.components.map((c) => c.componentName)).toEqual(['Thân']);
    expect(rows.variants).toHaveLength(2);
    expect(rows.norms).toHaveLength(1);
    expect(rows.norms[0]?.componentKey).toBe(rows.components[0]?.key);
    expect(rows.norms[0]?.variantKeys).toEqual([variantKey('Đen', 'M'), variantKey('Đen', 'L')]);
  });

  it('định mức (kích thước chi tiết) khác nhau giữa các size thì tách dòng', () => {
    const rows = toRows({
      ...spec,
      norms: [norm(1, 'Thân', 'Đen', 'M', 60), norm(2, 'Thân', 'Đen', 'L', 62)],
    });

    expect(rows.norms).toHaveLength(2);
  });

  it('toPayload nở mỗi dòng ra từng biến thể được chọn', () => {
    const rows = toRows(spec);

    const payload = toPayload(rows.components, rows.variants, rows.norms);

    expect(payload.norms.map((n) => `${n.color}-${n.sizeCode}:${n.lengthCm}`)).toEqual([
      'Đen-M:60',
      'Đen-L:60',
    ]);
    expect(payload.norms.every((n) => n.componentName === 'Thân')).toBe(true);
    expect(payload.variants.map((v) => v.productVariantId)).toEqual([10, 11]);
  });

  it('xóa thành phần thì xóa luôn các dòng định mức dùng nó và cho biết đã bỏ bao nhiêu dòng', () => {
    const rows = toRows(spec);
    const other: ComponentRow = {
      key: 'other',
      productComponentId: null,
      componentName: 'Cổ',
      componentType: 'Panel',
      piecesPerGarment: 1,
    };
    const keepNorm: NormRow = { ...(rows.norms[0] as NormRow), key: 'keep', componentKey: 'other' };

    const next = removeComponent(
      [...rows.components, other],
      [...rows.norms, keepNorm],
      (rows.components[0] as ComponentRow).key,
    );

    expect(next.components.map((c) => c.key)).toEqual(['other']);
    expect(next.norms.map((n) => n.key)).toEqual(['keep']);
    expect(next.removedNorms).toBe(1);
  });

  describe('validate', () => {
    const base = toRows(spec);
    const first = base.norms[0] as NormRow;
    const row = (change: Partial<NormRow>): NormRow => ({ ...first, ...change });

    it('hợp lệ với dữ liệu từ máy chủ', () => {
      expect(validate(base.components, base.variants, base.norms)).toBe('');
    });

    it('bắt dòng thiếu thành phần / NVL, không áp dụng cho biến thể nào', () => {
      expect(validate(base.components, base.variants, [row({ componentKey: undefined })])).not.toBe(
        '',
      );
      expect(validate(base.components, base.variants, [row({ materialId: undefined })])).not.toBe(
        '',
      );
      expect(validate(base.components, base.variants, [row({ variantKeys: [] })])).not.toBe('');
    });

    it('bắt trùng NVL cùng thành phần và màu – size, trùng tên thành phần, trùng màu – size', () => {
      expect(
        validate(base.components, base.variants, [row({ key: 'a' }), row({ key: 'b' })]),
      ).not.toBe('');
      expect(
        validate(
          [...base.components, { ...(base.components[0] as ComponentRow), key: 'x' }],
          base.variants,
          [],
        ),
      ).not.toBe('');
      expect(
        validate(
          base.components,
          [...base.variants, { ...(base.variants[0] as VariantRow), key: 'y' }],
          [],
        ),
      ).not.toBe('');
    });

    const withComponent = (change: Partial<ComponentRow>): ComponentRow[] => [
      { ...(base.components[0] as ComponentRow), ...change },
      ...base.components.slice(1),
    ];

    it.each([0.01, 99999.99, null])('kích thước chi tiết %s hợp lệ', (size) => {
      expect(
        validate(base.components, base.variants, [row({ lengthCm: size, widthCm: size })]),
      ).toBe('');
    });

    it.each([0, 100000])('kích thước chi tiết %s bị chặn', (size) => {
      expect(validate(base.components, base.variants, [row({ lengthCm: size })])).toContain(
        'chiều dài chi tiết',
      );
      expect(validate(base.components, base.variants, [row({ widthCm: size })])).toContain(
        'chiều rộng chi tiết',
      );
    });

    it.each([1, 99])('số chi tiết trên một sản phẩm %s hợp lệ', (pieces) => {
      expect(validate(withComponent({ piecesPerGarment: pieces }), base.variants, [])).toBe('');
    });

    it.each([0, 100, -1])('số chi tiết trên một sản phẩm %s bị chặn', (pieces) => {
      expect(validate(withComponent({ piecesPerGarment: pieces }), base.variants, [])).toContain(
        'từ 1 đến 99',
      );
    });

    it('đổi tên thành phần thì dòng định mức đi theo, không bị coi là mất liên kết', () => {
      const renamed = withComponent({ componentName: 'Thân mới' });

      expect(validate(renamed, base.variants, base.norms)).toBe('');
      expect(
        toPayload(renamed, base.variants, base.norms).norms.every(
          (n) => n.componentName === 'Thân mới',
        ),
      ).toBe(true);
    });

    it('thành phần bỏ trống tên thì báo thiếu tên, dòng định mức vẫn giữ liên kết', () => {
      expect(validate(withComponent({ componentName: '  ' }), base.variants, base.norms)).toContain(
        'phải có tên',
      );
    });

    it('dòng định mức trỏ vào thành phần không còn thì báo chọn lại thành phần', () => {
      expect(validate([], base.variants, [row({})])).toContain('chọn thành phần');
    });

    const manyComponents = (count: number): ComponentRow[] =>
      Array.from({ length: count }, (_, i) => ({
        key: `c${i}`,
        productComponentId: null,
        componentName: `TP${i}`,
        componentType: 'Panel' as const,
        piecesPerGarment: 1,
      }));
    const manyVariants = (count: number): VariantRow[] =>
      Array.from({ length: count }, (_, i) => ({
        key: `v${i}`,
        productVariantId: i + 1,
        color: `C${i}`,
        sizeCode: 'M',
      }));
    const manyNorms = (count: number, variants: VariantRow[]): NormRow[] =>
      Array.from({ length: count }, (_, i) => ({
        key: `n${i}`,
        componentKey: (base.components[0] as ComponentRow).key,
        materialId: i + 1,
        lengthCm: null,
        widthCm: null,
        specNote: '',
        variantKeys: variants.map((v) => variantKey(v.color, v.sizeCode)),
      }));

    it('số thành phần và màu – size: đúng 200 thì qua, 201 thì bị chặn', () => {
      expect(validate(manyComponents(200), base.variants, [])).toBe('');
      expect(validate(manyComponents(201), base.variants, [])).toContain('Tối đa 200 thành phần');
      expect(validate(base.components, manyVariants(200), [])).toBe('');
      expect(validate(base.components, manyVariants(201), [])).toContain('Tối đa 200 màu');
    });

    it('số dòng định mức tính sau khi nở theo màu – size: đúng 5.000 thì qua, 5.001 thì bị chặn', () => {
      const variants = manyVariants(200);
      const atLimit = manyNorms(25, variants);
      const oneMore = {
        ...(manyNorms(26, variants)[25] as NormRow),
        variantKeys: [variantKey('C0', 'M')],
      };

      expect(validate(base.components, variants, atLimit)).toBe('');
      expect(validate(base.components, variants, [...atLimit, oneMore])).toContain('5.001 dòng');
    });
  });
});
