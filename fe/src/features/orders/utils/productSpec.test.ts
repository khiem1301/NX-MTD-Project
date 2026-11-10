import type { ProductSpec } from '../types';
import {
  panelFabricProblem,
  removeComponent,
  toMaterialOption,
  toPayload,
  toRows,
  colorKey,
  validate,
  type ComponentRow,
  type NormRow,
  type VariantRow,
} from './productSpec';

const norm = (
  id: number,
  component: string,
  color: string,
  note: string | null = null,
  material = 1,
) => ({
  productComponentMaterialId: id,
  componentName: component,
  color,
  materialId: material,
  materialCode: 'V1',
  materialName: 'Vải',
  unit: 'm',
  materialColor: 'Đen',
  materialType: 'FABRIC',
  specNote: note,
});

const spec: ProductSpec = {
  productId: 1,
  markerComponents: [],
  markerFabrics: [],
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
  // Định mức theo màu (DEV-ALL-87): một dòng cho màu Đen dùng chung size M, L; màu Đỏ đã ngừng dùng.
  norms: [norm(1, 'Thân', 'Đen', 'may kép'), norm(3, 'Cổ cũ', 'Đen'), norm(4, 'Thân', 'Đỏ')],
};

describe('productSpec', () => {
  it('mỗi màu một dòng định mức và bỏ thành phần / màu đã ngừng dùng', () => {
    const rows = toRows(spec);

    expect(rows.components.map((c) => c.componentName)).toEqual(['Thân']);
    expect(rows.variants).toHaveLength(2);
    expect(rows.norms).toHaveLength(1);
    expect(rows.norms[0]?.componentKey).toBe(rows.components[0]?.key);
    expect(rows.norms[0]?.colorKeys).toEqual([colorKey('Đen')]);
  });

  it('cùng định mức ở nhiều màu thì gộp một dòng, khác ghi chú quy cách thì tách dòng', () => {
    const twoColors = {
      ...spec,
      variants: [
        ...spec.variants.slice(0, 2),
        { productVariantId: 13, color: 'Xanh', sizeCode: 'M', status: 'Active' },
      ],
    };
    const same = toRows({
      ...twoColors,
      norms: [norm(1, 'Thân', 'Đen', 'may kép'), norm(2, 'Thân', 'Xanh', 'may kép')],
    });
    const different = toRows({
      ...twoColors,
      norms: [norm(1, 'Thân', 'Đen', 'may kép'), norm(2, 'Thân', 'Xanh', 'may đơn')],
    });

    expect(same.norms).toHaveLength(1);
    expect(same.norms[0]?.colorKeys).toEqual([colorKey('Đen'), colorKey('Xanh')]);
    expect(different.norms).toHaveLength(2);
  });

  it('toPayload nở mỗi dòng ra từng màu được chọn, không theo size', () => {
    const rows = toRows(spec);

    const payload = toPayload(rows.components, rows.variants, rows.norms);

    expect(payload.norms.map((n) => `${n.color}:${n.specNote}`)).toEqual(['Đen:may kép']);
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
      expect(validate(base.components, base.variants, [row({ colorKeys: [] })])).not.toBe('');
    });

    it('bắt trùng NVL cùng thành phần và màu, trùng tên thành phần, trùng màu – size', () => {
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
        specNote: '',
        colorKeys: variants.map((v) => colorKey(v.color)),
      }));

    it('số thành phần và màu – size: đúng 200 thì qua, 201 thì bị chặn', () => {
      expect(validate(manyComponents(200), base.variants, [])).toBe('');
      expect(validate(manyComponents(201), base.variants, [])).toContain('Tối đa 200 thành phần');
      expect(validate(base.components, manyVariants(200), [])).toBe('');
      expect(validate(base.components, manyVariants(201), [])).toContain('Tối đa 200 màu');
    });

    it('số dòng định mức tính sau khi nở theo màu: đúng 5.000 thì qua, 5.001 thì bị chặn', () => {
      const variants = manyVariants(200);
      const atLimit = manyNorms(25, variants);
      const oneMore = {
        ...(manyNorms(26, variants)[25] as NormRow),
        colorKeys: [colorKey('C0')],
      };

      expect(validate(base.components, variants, atLimit)).toBe('');
      expect(validate(base.components, variants, [...atLimit, oneMore])).toContain('5.001 dòng');
    });
  });
});

describe('panelFabricProblem (DEV-ALL-86)', () => {
  const material = (materialId: number, materialType: string) =>
    toMaterialOption({
      materialId,
      materialCode: `M${materialId}`,
      materialName: 'NVL',
      color: '',
      unit: 'm',
      materialType,
    });
  const materials = [material(1, 'FABRIC'), material(2, 'FABRIC'), material(3, 'TRIM')];
  const variants: VariantRow[] = [{ key: 'v1', productVariantId: 1, color: 'Đen', sizeCode: 'M' }];
  const component = (key: string, componentType: 'Panel' | 'Trim'): ComponentRow => ({
    key,
    productComponentId: null,
    componentName: key,
    componentType,
    piecesPerGarment: 1,
  });
  const norm = (componentKey: string, materialId: number): NormRow => ({
    key: `${componentKey}-${materialId}`,
    componentKey,
    materialId,
    specNote: '',
    colorKeys: [colorKey('Đen')],
  });
  const check = (components: ComponentRow[], norms: NormRow[]) =>
    panelFabricProblem(components, variants, norms, materials);

  it('chi tiết cắt khai hai vải ở cùng màu thì báo, kèm tên chi tiết và màu', () => {
    expect(check([component('Tay áo', 'Panel')], [norm('Tay áo', 1), norm('Tay áo', 2)])).toBe(
      'Chi tiết cắt "Tay áo" màu Đen khai hơn một vải. Mỗi chi tiết cắt chỉ một vải: tách vải lót, mex, bo thành chi tiết riêng (vd "Lót Tay áo", "Mex Tay áo").',
    );
  });

  it('vải lót là chi tiết riêng, phụ liệu trên chi tiết cắt, hoặc phụ liệu nhiều NVL thì hợp lệ', () => {
    const components = [
      component('Tay áo', 'Panel'),
      component('Lót', 'Panel'),
      component('Khóa', 'Trim'),
    ];

    expect(
      check(components, [
        norm('Tay áo', 1),
        norm('Tay áo', 3),
        norm('Lót', 2),
        norm('Khóa', 1),
        norm('Khóa', 2),
      ]),
    ).toBe('');
  });
});
