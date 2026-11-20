import type { SpecOrderMarkerExcelRow } from '../types';
import { excelBundleApi } from '../api/orderApi';
import {
  bundleOfOrderKind,
  groupMarkersByProduct,
  hasOrderImports,
  pickOrderData,
  runOrderImports,
  takeOrderKind,
} from './orderExcelImports';
import { EMPTY_BUNDLE } from './productExcelImports';

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

const marker = (rowNumber: number, productCode: string | null): SpecOrderMarkerExcelRow => ({
  rowNumber,
  parseError: null,
  productCode,
  markerNo: 1,
  markerCode: `1A${rowNumber}`,
  lengthM: 2,
  widthM: 1.5,
  status: 'ACTIVE',
  ratios: [{ sizeCode: 'M', piecesPerPly: 1 }],
});

const material = {
  rowNumber: 4,
  parseError: null,
  materialCode: 'V-01',
  requiredQty: 10,
  customerQty: null,
  note: null,
};

const products = [
  { productId: 7, productCode: 'AO-01' },
  { productId: 8, productCode: 'QU-02' },
];

describe('orderExcelImports', () => {
  beforeEach(() => jest.clearAllMocks());

  it('pickOrderData lấy đúng bảng của mục; file không có bảng thì trả null', () => {
    const bundle = {
      ...EMPTY_BUNDLE,
      spec: { ...EMPTY_BUNDLE.spec, orderMarkers: [marker(5, 'AO-01')], sizeColumns: ['M'] },
    };

    const picked = pickOrderData({}, bundle, 'markers', 'bia.xlsx');

    expect(picked?.markers).toEqual({
      fileName: 'bia.xlsx',
      rows: bundle.spec.orderMarkers,
      sizeColumns: ['M'],
    });
    expect(pickOrderData({}, bundle, 'orderMaterials', 'bia.xlsx')).toBeNull();
    expect(hasOrderImports(picked ?? {})).toBe(true);
    expect(hasOrderImports({})).toBe(false);
  });

  it('bundleOfOrderKind / takeOrderKind giữ đúng bảng đã sửa trên popup kiểm tra', () => {
    const rows = [marker(5, 'AO-01')];
    const selection = {
      markers: { fileName: 'bia.xlsx', rows, sizeColumns: ['M'] },
    };
    const bundle = bundleOfOrderKind(selection, 'markers');
    expect(bundle?.spec.orderMarkers).toEqual(rows);
    expect(bundleOfOrderKind({}, 'markers')).toBeNull();

    const edited = {
      ...EMPTY_BUNDLE,
      spec: {
        ...EMPTY_BUNDLE.spec,
        hasOrderMarkers: true,
        orderMarkers: [marker(5, 'AO-01'), marker(6, 'QU-02')],
        sizeColumns: ['M', 'L'],
      },
    };
    expect(takeOrderKind(selection, 'markers', 'bia2.xlsx', edited).markers).toEqual({
      fileName: 'bia2.xlsx',
      rows: edited.spec.orderMarkers,
      sizeColumns: ['M', 'L'],
    });
  });

  it('groupMarkersByProduct chia theo mã hàng; mã lạ hoặc thiếu mã khi đơn nhiều mã hàng thì bỏ qua', () => {
    const { groups, skipped } = groupMarkersByProduct(
      [marker(5, 'ao-01'), marker(6, 'QU-02'), marker(7, 'XX'), marker(8, null)],
      products,
    );

    expect(groups.get(7)?.map((r) => r.rowNumber)).toEqual([5]);
    expect(groups.get(8)?.map((r) => r.rowNumber)).toEqual([6]);
    expect(skipped).toHaveLength(2);
  });

  it('groupMarkersByProduct: đơn một mã hàng thì dòng không ghi mã hàng thuộc mã đó', () => {
    const { groups, skipped } = groupMarkersByProduct([marker(5, null)], [products[0]!]);

    expect(groups.get(7)).toHaveLength(1);
    expect(skipped).toEqual([]);
  });

  it('runOrderImports nhập NVL của đơn rồi bìa của từng mã hàng vào đơn vừa lưu', async () => {
    api.previewOrderMaterials.mockResolvedValue({
      rows: [],
      validCount: 1,
      errorCount: 0,
      duplicateCount: 0,
      changeCount: 1,
    });
    api.importOrderMaterials.mockResolvedValue({
      success: true,
      data: null,
      message: 'Đã nhập 1 NVL',
    });
    api.previewSpec.mockResolvedValue({
      orderMarkers: [],
      notes: [],
      canImport: true,
      crossErrors: [],
    } as never);
    api.importSpec.mockResolvedValue({ success: true, data: {} as never, message: 'Đã nhập bìa' });

    const outcomes = await runOrderImports(
      30,
      {
        orderMaterials: { fileName: 'nvl.xlsx', rows: [material] },
        markers: { fileName: 'bia.xlsx', rows: [marker(5, 'QU-02')], sizeColumns: ['M'] },
      },
      products,
    );

    expect(api.importOrderMaterials).toHaveBeenCalledWith(30, [material]);
    expect(api.importSpec).toHaveBeenCalledWith(
      8,
      expect.objectContaining({ hasOrderMarkers: true }),
      30,
      ['markers'],
    );
    expect(outcomes.map((o) => [o.kind, o.label, o.ok])).toEqual([
      ['orderMaterials', undefined, true],
      ['markers', 'QU-02', true],
    ]);
  });
});
