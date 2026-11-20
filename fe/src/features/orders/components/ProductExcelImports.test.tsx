import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import { ProductExcelImports } from './ProductExcelImports';
import type { ExcelBundle } from '../types';
import type { ProductImportSelection } from '../utils/productExcelImports';

const parsed: ExcelBundle = {
  hasProducts: true,
  products: [{ rowNumber: 5, productCode: 'TEST01', description: null }],
  spec: {
    hasComponents: true,
    components: [
      { rowNumber: 5, name: 'Cổ', componentType: 'PANEL', piecesPerGarment: 1, parseError: null },
    ],
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
};

jest.mock('../api/orderApi', () => ({
  excelBundleApi: { parse: jest.fn(() => Promise.resolve(parsed)) },
}));
jest.mock('@/services/materialApi', () => ({ materialApi: {} }));
jest.mock('@/services/queryClient', () => ({ queryClient: {} }));
jest.mock('../hooks/useSpecImport', () => ({
  useProductOrders: () => ({ data: { items: [] }, isLoading: false }),
}));
// Popup thật có bảng sửa và gọi API kiểm; ở đây chỉ cần biết nó mở và Xác nhận trả dữ liệu.
jest.mock('./ExcelReviewModal', () => ({
  ExcelReviewModal: ({
    bundle,
    only,
    onConfirm,
  }: {
    bundle: ExcelBundle;
    only?: string[];
    onConfirm: (b: ExcelBundle, materials: object[]) => void;
  }) => (
    <div>
      <span>popup:{only?.join(',') ?? 'all'}</span>
      <button onClick={() => onConfirm(bundle, [])}>xác nhận</button>
    </div>
  ),
}));

const wrap = (ui: ReactNode) => (
  <QueryClientProvider client={new QueryClient()}>{ui}</QueryClientProvider>
);

describe('ProductExcelImports', () => {
  it('Chọn 1 file chung: đọc file, mở popup, Xác nhận thì giữ dữ liệu và điền dòng Mã hàng', async () => {
    const onChange = jest.fn();
    const onProductRow = jest.fn();
    const { container } = render(
      wrap(
        <ProductExcelImports deferred value={{}} onChange={onChange} onProductRow={onProductRow} />,
      ),
    );
    const inputs = container.querySelectorAll('input[type="file"]');
    fireEvent.change(inputs[0] as Element, { target: { files: [new File(['x'], 'all.xlsx')] } });

    await screen.findByText('popup:all');
    fireEvent.click(screen.getByText('xác nhận'));

    const next = onChange.mock.calls[0][0] as ProductImportSelection;
    expect(next.fileName).toBe('all.xlsx');
    expect(next.bundle?.spec.components).toHaveLength(1);
    expect(onProductRow).toHaveBeenCalledWith(parsed.products[0]);
  });

  it('Chọn file của một mục: popup chỉ có bảng của mục đó', async () => {
    const onChange = jest.fn();
    const { container } = render(
      wrap(<ProductExcelImports deferred value={{}} onChange={onChange} />),
    );
    const inputs = container.querySelectorAll('input[type="file"]');
    fireEvent.change(inputs[1] as Element, { target: { files: [new File(['x'], 'tp.xlsx')] } });

    await waitFor(() => expect(screen.getByText('popup:components')).toBeTruthy());
  });
});
