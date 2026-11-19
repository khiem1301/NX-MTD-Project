import {
  EMPTY_BUNDLE,
  clearKind,
  missingKinds,
  requiredKindsFor,
  kindsWithData,
  runProductImports,
  takeKind,
} from './productExcelImports';
import { excelBundleApi } from '../api/orderApi';
import { materialApi } from '@/services/materialApi';
import type { ExcelBundle } from '../types';

jest.mock('../api/orderApi', () => ({
  excelBundleApi: {
    previewSpec: jest.fn(),
    importSpec: jest.fn(),
    previewOrderMaterials: jest.fn(),
    importOrderMaterials: jest.fn(),
  },
}));
jest.mock('@/services/materialApi', () => ({ materialApi: { create: jest.fn() } }));
jest.mock('@/services/queryClient', () => ({
  queryClient: { invalidateQueries: jest.fn().mockResolvedValue(undefined) },
}));

const api = excelBundleApi as jest.Mocked<typeof excelBundleApi>;

const preview = (over: object) => ({
  components: [],
  norms: [],
  markerGroups: [],
  orderMarkers: [],
  crossErrors: [],
  notes: [],
  canImport: true,
  ...over,
});

const bundle: ExcelBundle = {
  ...EMPTY_BUNDLE,
  spec: {
    ...EMPTY_BUNDLE.spec,
    hasComponents: true,
    components: [
      { rowNumber: 5, name: 'Cổ', componentType: 'PANEL', piecesPerGarment: 1, parseError: null },
    ],
    hasNorms: true,
    norms: [
      {
        rowNumber: 5,
        color: 'Tím',
        componentName: 'Cổ',
        materialCode: 'V-1',
        specNote: null,
        parseError: null,
      },
    ],
    hasOrderMarkers: true,
    orderMarkers: [
      {
        rowNumber: 5,
        productCode: null,
        markerNo: 1,
        markerCode: '1A',
        lengthM: 4,
        widthM: 1.5,
        status: 'ACTIVE',
        ratios: [],
        parseError: null,
      },
    ],
    sizeColumns: ['S'],
  },
  hasOrderMaterials: true,
  orderMaterials: [
    {
      rowNumber: 5,
      materialCode: 'V-1',
      requiredQty: 10,
      customerQty: null,
      note: null,
      parseError: null,
    },
  ],
};

describe('runProductImports', () => {
  beforeEach(() => jest.clearAllMocks());

  it('nhập từng bảng riêng theo thứ tự bằng dữ liệu đã sửa; chưa có đơn thì bỏ qua bìa, NVL', async () => {
    api.previewSpec.mockImplementation(async (_id, workbook) =>
      workbook.hasNorms
        ? (preview({
            canImport: false,
            norms: [{ rowNumber: 5, action: 'Error', error: 'Màu "Tím" chưa có' }],
          }) as never)
        : (preview({}) as never),
    );
    api.importSpec.mockResolvedValue({ success: true, data: {} as never, message: 'Đã nhập' });

    const outcomes = await runProductImports(9, { bundle });

    expect(outcomes.map((o) => [o.kind, o.ok, o.skipped ?? false])).toEqual([
      ['components', true, false],
      ['norms', false, false],
      ['markers', false, true],
      ['orderMaterials', false, true],
    ]);
    expect(outcomes[1]?.rowErrors[0]).toContain('Dòng 5');
    // Mỗi lần chỉ gửi bảng của mục đó.
    const firstWorkbook = api.previewSpec.mock.calls[0]?.[1];
    expect(firstWorkbook?.hasComponents).toBe(true);
    expect(firstWorkbook?.hasNorms).toBe(false);
    expect(api.importSpec).toHaveBeenCalledTimes(1);
  });

  it('có đơn thì nhập bìa cắt và NVL của đơn theo đơn đó', async () => {
    api.previewSpec.mockResolvedValue(preview({}) as never);
    api.importSpec.mockResolvedValue({ success: true, data: {} as never, message: 'OK' });
    api.previewOrderMaterials.mockResolvedValue({
      rows: [],
      validCount: 1,
      errorCount: 0,
      duplicateCount: 0,
      changeCount: 1,
    });
    api.importOrderMaterials.mockResolvedValue({ success: true, data: null, message: 'OK' });

    const outcomes = await runProductImports(9, { bundle, orderId: 23 });

    expect(outcomes.every((o) => o.ok)).toBe(true);
    expect(api.previewSpec).toHaveBeenCalledWith(
      9,
      expect.objectContaining({ hasOrderMarkers: true }),
      23,
      ['markers'],
    );
    expect(api.importOrderMaterials).toHaveBeenCalledWith(23, bundle.orderMaterials);
  });

  it('lỗi máy chủ của một mục không chặn mục sau', async () => {
    api.previewSpec
      .mockRejectedValueOnce({ success: false, errorCode: 'X', message: 'Lỗi máy chủ' })
      .mockResolvedValue(preview({}) as never);
    api.importSpec.mockResolvedValue({ success: true, data: {} as never, message: 'Đã nhập' });

    const outcomes = await runProductImports(9, {
      bundle: clearKind(clearKind(bundle, 'markers'), 'orderMaterials'),
    });

    expect(outcomes.map((o) => [o.kind, o.ok, o.message])).toEqual([
      ['components', false, 'Lỗi máy chủ'],
      ['norms', true, 'Đã nhập'],
    ]);
  });
});

describe('NVL mới vào danh mục (DEV-ALL-123)', () => {
  beforeEach(() => jest.clearAllMocks());

  it('tạo NVL trước rồi mới nhập; NVL lỗi báo riêng, không chặn', async () => {
    const order: string[] = [];
    (materialApi.create as jest.Mock).mockImplementation(
      async (_role, m: { materialCode: string }) => {
        order.push(`create ${m.materialCode}`);
        if (m.materialCode === 'X2')
          throw { success: false, errorCode: 'DUP', message: 'Mã đã có' };
        return {};
      },
    );
    api.previewSpec.mockImplementation(async () => {
      order.push('preview');
      return preview({}) as never;
    });
    api.importSpec.mockResolvedValue({ success: true, data: {} as never, message: 'OK' });
    const material = {
      materialName: 'Vải mới',
      materialType: 'FABRIC',
      unit: 'm',
    };

    const outcomes = await runProductImports(9, {
      bundle: clearKind(clearKind(clearKind(bundle, 'markers'), 'orderMaterials'), 'norms'),
      newMaterials: [
        { ...material, materialCode: 'X1' },
        { ...material, materialCode: 'X2' },
      ],
    });

    expect(order.slice(0, 3)).toEqual(['create X1', 'create X2', 'preview']);
    expect(outcomes[0]?.kind).toBe('materials');
    expect(outcomes[0]?.message).toContain('X1');
    expect(outcomes[0]?.rowErrors[0]).toContain('Mã đã có');
  });
});

describe('mục bắt buộc khi Thêm mới (DEV-ALL-124)', () => {
  it('form đơn bắt buộc đủ 5 mục; màn Mã hàng 3 mục định mức; Sửa không bắt buộc', () => {
    expect(requiredKindsFor(true, true)).toEqual([
      'components',
      'norms',
      'groups',
      'markers',
      'orderMaterials',
    ]);
    expect(requiredKindsFor(true, false)).toEqual(['components', 'norms', 'groups']);
    expect(requiredKindsFor(false, true)).toEqual([]);
  });

  it('liệt kê mục bắt buộc chưa có dòng nào', () => {
    const noGroups = clearKind(bundle, 'groups');
    expect(missingKinds({ bundle: noGroups }, requiredKindsFor(true, true))).toEqual(['groups']);
    expect(missingKinds({}, ['components'])).toEqual(['components']);
  });
});

describe('bộ dữ liệu', () => {
  it('chọn file riêng cho một mục chỉ thay bảng của mục đó', () => {
    const other: ExcelBundle = {
      ...EMPTY_BUNDLE,
      spec: { ...EMPTY_BUNDLE.spec, hasComponents: true, components: [] },
      hasOrderMaterials: true,
      orderMaterials: [],
    };
    const merged = takeKind(bundle, other, 'components');

    expect(merged.spec.components).toEqual([]);
    expect(merged.spec.norms).toBe(bundle.spec.norms);
    expect(merged.orderMaterials).toBe(bundle.orderMaterials);
    expect(kindsWithData(merged)).toEqual(['norms', 'markers', 'orderMaterials']);
  });
});
