import type { ProductSpec } from '../types';
import { toPayload, toRows, validate, variantKey, type ComponentRow, type NormRow, type VariantRow } from './productSpec';

const norm = (id: number, component: string, color: string, size: string, qty = 1, material = 1) => ({
  productComponentMaterialId: id,
  componentName: component,
  color,
  sizeCode: size,
  materialId: material,
  materialCode: 'V1',
  materialName: 'Vải',
  unit: 'm',
  materialColor: 'Đen',
  qtyPerGarment: qty,
  wastagePercent: 5,
  lengthCm: null,
  widthCm: null,
  specNote: null,
});

const spec: ProductSpec = {
  productId: 1,
  components: [
    { productComponentId: 1, componentName: 'Thân', componentType: 'Panel', piecesPerGarment: 1, sortOrder: 1, status: 'Active' },
    { productComponentId: 2, componentName: 'Cổ cũ', componentType: 'Panel', piecesPerGarment: 1, sortOrder: 2, status: 'Discontinued' },
  ],
  variants: [
    { productVariantId: 10, color: 'Đen', sizeCode: 'M', status: 'Active' },
    { productVariantId: 11, color: 'Đen', sizeCode: 'L', status: 'Active' },
    { productVariantId: 12, color: 'Đỏ', sizeCode: 'S', status: 'Discontinued' },
  ],
  norms: [
    norm(1, 'Thân', 'Đen', 'M', 1.2),
    norm(2, 'Thân', 'Đen', 'L', 1.2),
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
    expect(rows.norms[0]?.variantKeys).toEqual([variantKey('Đen', 'M'), variantKey('Đen', 'L')]);
  });

  it('định mức khác nhau giữa các size thì tách dòng', () => {
    const rows = toRows({ ...spec, norms: [norm(1, 'Thân', 'Đen', 'M', 1.2), norm(2, 'Thân', 'Đen', 'L', 1.4)] });

    expect(rows.norms).toHaveLength(2);
  });

  it('toPayload nở mỗi dòng ra từng biến thể được chọn', () => {
    const rows = toRows(spec);

    const payload = toPayload(rows.components, rows.variants, rows.norms);

    expect(payload.norms.map((n) => `${n.color}-${n.sizeCode}:${n.qtyPerGarment}`)).toEqual(['Đen-M:1.2', 'Đen-L:1.2']);
    expect(payload.norms.every((n) => n.componentName === 'Thân' && n.materialColor === 'Đen')).toBe(true);
    expect(payload.variants.map((v) => v.productVariantId)).toEqual([10, 11]);
  });

  describe('validate', () => {
    const base = toRows(spec);
    const first = base.norms[0] as NormRow;
    const row = (change: Partial<NormRow>): NormRow => ({ ...first, ...change });

    it('hợp lệ với dữ liệu từ máy chủ', () => {
      expect(validate(base.components, base.variants, base.norms)).toBe('');
    });

    it('bắt dòng thiếu thành phần / NVL, định mức không dương, không áp dụng cho biến thể nào', () => {
      expect(validate(base.components, base.variants, [row({ componentName: undefined })])).not.toBe('');
      expect(validate(base.components, base.variants, [row({ materialId: undefined })])).not.toBe('');
      expect(validate(base.components, base.variants, [row({ qtyPerGarment: 0 })])).not.toBe('');
      expect(validate(base.components, base.variants, [row({ variantKeys: [] })])).not.toBe('');
    });

    it('bắt trùng NVL cùng thành phần và màu – size, trùng tên thành phần, trùng màu – size', () => {
      expect(validate(base.components, base.variants, [row({ key: 'a' }), row({ key: 'b' })])).not.toBe('');
      expect(validate([...base.components, { ...(base.components[0] as ComponentRow), key: 'x' }], base.variants, [])).not.toBe('');
      expect(validate(base.components, [...base.variants, { ...(base.variants[0] as VariantRow), key: 'y' }], [])).not.toBe('');
    });
  });
});
