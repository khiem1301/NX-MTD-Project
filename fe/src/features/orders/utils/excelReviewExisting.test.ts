import type { ExcelBundle, ProductSpec } from '../types';
import { mergeExisting } from './excelReviewExisting';

const emptyBundle: ExcelBundle = {
  hasProducts: false,
  products: [],
  spec: {
    hasComponents: false,
    components: [],
    hasNorms: false,
    norms: [],
    hasMarkerGroups: false,
    markerGroups: [],
    hasOrderMarkers: false,
    orderMarkers: [],
    sizeColumns: [],
  },
  hasOrderMaterials: false,
  orderMaterials: [],
  hasMaterials: false,
  materials: [],
};

const spec: ProductSpec = {
  productId: 1,
  components: [
    { productComponentId: 1, componentName: 'Thân', componentType: 'Panel', piecesPerGarment: 1, status: 'Active' },
    { productComponentId: 2, componentName: 'Chỉ', componentType: 'Trim', piecesPerGarment: 1, status: 'Active' },
  ],
  variants: [],
  norms: [
    {
      productComponentMaterialId: 1,
      componentName: 'Thân',
      color: 'Trắng',
      materialId: 5,
      materialCode: 'VAI-A',
      materialName: 'Vải A',
      unit: 'm',
      materialColor: '',
      specNote: null,
      materialType: 'FABRIC',
    },
  ],
  markerComponents: [{ markerNo: 1, componentName: 'Thân' }],
  markerFabrics: [
    { markerNo: 1, color: 'Trắng', materialId: 5, materialCode: 'VAI-A', materialName: 'Vải A', materialColor: '' },
  ],
};

describe('mergeExisting', () => {
  it('thêm dòng đang có cho bảng chưa có trong file, đánh số tiếp và bật cờ Has', () => {
    const { bundle, loaded } = mergeExisting(emptyBundle, { spec }, ['components', 'norms', 'groups']);
    expect(loaded).toEqual(['components', 'norms', 'groups']);
    expect(bundle.spec.hasComponents && bundle.spec.hasNorms && bundle.spec.hasMarkerGroups).toBe(true);
    expect(bundle.spec.components.map((c) => [c.name, c.componentType, c.rowNumber])).toEqual([
      ['Thân', 'PANEL', 1],
      ['Chỉ', 'TRIM', 2],
    ]);
    expect(bundle.spec.norms[0]).toMatchObject({ color: 'Trắng', materialCode: 'VAI-A', rowNumber: 3 });
    expect(bundle.spec.markerGroups[0]).toMatchObject({ markerNo: 1, componentNames: ['Thân'], colors: ['Trắng'] });
  });

  it('giữ dòng của file khi trùng khóa và không nạp lại lần hai', () => {
    const withFile: ExcelBundle = {
      ...emptyBundle,
      spec: {
        ...emptyBundle.spec,
        hasNorms: true,
        norms: [
          { rowNumber: 2, color: 'trắng', componentName: 'THÂN', materialCode: 'VAI-B', specNote: null, parseError: null },
        ],
      },
    };
    const first = mergeExisting(withFile, { spec }, ['norms']).bundle;
    expect(first.spec.norms).toHaveLength(1);
    expect(first.spec.norms[0]?.materialCode).toBe('VAI-B');
    const again = mergeExisting(first, { spec }, ['components']).bundle;
    expect(mergeExisting(again, { spec }, ['components']).bundle.spec.components).toHaveLength(2);
  });
});
