import { EMPTY_BUNDLE } from './productExcelImports';
import {
  addRow,
  defaultMaterial,
  incompleteMaterials,
  materialTypeDefaults,
  patchRow,
  removeRow,
  rowStateOf,
} from './excelReview';
import type { ExcelBundle } from '../types';

// productExcelImports (nơi có EMPTY_BUNDLE) kéo theo API; test này chỉ dùng hàm thuần.
jest.mock('../api/orderApi', () => ({ excelBundleApi: {} }));
jest.mock('@/services/materialApi', () => ({ materialApi: {} }));
jest.mock('@/services/queryClient', () => ({ queryClient: {} }));

const bundle: ExcelBundle = {
  ...EMPTY_BUNDLE,
  spec: {
    ...EMPTY_BUNDLE.spec,
    hasComponents: true,
    components: [
      { rowNumber: 5, name: 'Cổ', componentType: 'PANEL', piecesPerGarment: 1, parseError: 'sai' },
    ],
  },
  hasOrderMaterials: true,
  orderMaterials: [
    {
      rowNumber: 9,
      materialCode: 'X',
      requiredQty: 1,
      customerQty: null,
      note: null,
      parseError: null,
    },
  ],
};

describe('excelReview (DEV-ALL-122/123)', () => {
  it('sửa dòng gộp giá trị và xóa lỗi định dạng lúc đọc file', () => {
    const next = patchRow(bundle, 'components', 5, { name: 'Thân' });
    expect(next.spec.components[0]).toMatchObject({ name: 'Thân', parseError: null });
    expect(next.orderMaterials).toBe(bundle.orderMaterials);
  });

  it('thêm dòng lấy số dòng lớn hơn mọi bảng; xóa dòng theo số dòng', () => {
    const added = addRow(bundle, 'components');
    expect(added.spec.components.map((c) => c.rowNumber)).toEqual([5, 10]);
    expect(removeRow(added, 'components', 5).spec.components.map((c) => c.rowNumber)).toEqual([10]);
  });

  it('phân loại dòng theo kết quả kiểm; lỗi định dạng khi chưa kiểm là lỗi', () => {
    expect(rowStateOf({ action: 'Error', error: 'x' })).toBe('error');
    expect(rowStateOf({ action: 'NewMaterial', error: 'x' })).toBe('newMaterial');
    expect(rowStateOf({ action: 'Pending', error: 'x' })).toBe('pending');
    expect(rowStateOf({ action: 'New', error: null })).toBe('valid');
    expect(rowStateOf(undefined, 'sai')).toBe('error');
  });

  it('gợi ý NVL mới theo loại; thiếu tên / khổ thì chưa đủ thông tin', () => {
    const fabric = defaultMaterial({
      materialCode: 'LUA',
      suggestedType: 'FABRIC',
      colors: ['Hồng'],
      suggestedName: 'Lụa hồng',
    });
    expect(fabric).toMatchObject({ materialType: 'FABRIC', unit: 'm', color: 'Hồng', widthM: 1.5 });
    const trim = defaultMaterial({
      materialCode: 'CUC',
      suggestedType: 'TRIM',
      colors: [],
      suggestedUnit: 'hộp',
    });
    expect(trim).toMatchObject({
      materialType: 'TRIM',
      unit: 'hộp',
      widthM: null,
      materialName: '',
    });
    expect(incompleteMaterials([fabric, trim]).map((m) => m.materialCode)).toEqual(['CUC']);
    expect(materialTypeDefaults('TRIM', 2)).toMatchObject({ category: 'OTHER', widthM: null });
  });
});
