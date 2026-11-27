import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { ProductFormModal } from './ProductFormModal';
import type { ExcelBundle, ProductExcelRow } from '../types';
import type { ProductImportSelection } from '../utils/productExcelImports';

const mutate = jest.fn();
// Tên bắt đầu bằng "mock" để jest cho dùng trong jest.mock (hoisting).
const mockMissing = jest.fn((..._args: unknown[]) => [] as string[]);
jest.mock('../hooks/useOrders', () => ({
  useSaveProduct: () => ({ mutate, isPending: false }),
  useProduct: () => ({ data: undefined, error: null, refetch: jest.fn() }),
}));
jest.mock('../api/orderApi', () => ({
  excelBundleApi: {
    check: jest.fn(() =>
      Promise.resolve({
        products: null,
        orderMaterials: null,
        missingMaterials: [],
        spec: { components: [], norms: [], markerGroups: [], orderMarkers: [], crossErrors: [] },
      }),
    ),
  },
}));
jest.mock('./SampleImageInput', () => ({ SampleImageInput: () => null }));
jest.mock('./ExtraImagesInput', () => ({ ExtraImagesInput: () => null }));
jest.mock('../utils/productExcelImports', () => ({
  hasImportFiles: (s: { bundle?: object }) => s.bundle !== undefined,
  requiredKindsFor: () => ['components', 'norms', 'groups', 'markers', 'orderMaterials'],
  missingKinds: (...args: unknown[]) => mockMissing(...args),
  runProductImports: jest.fn(),
  showProductImportResults: jest.fn(),
}));

const bundle = {
  hasProducts: true,
  products: [],
  spec: { components: [], norms: [], markerGroups: [], orderMarkers: [], sizeColumns: [] },
  orderMaterials: [],
  hasMaterials: false,
  materials: [],
} as unknown as ExcelBundle;
const row: ProductExcelRow = {
  rowNumber: 5,
  productCode: 'test01',
  description: 'Mô tả từ Excel',
};

// Khung nhập thật đọc file và mở popup; ở đây giả lập bước Xác nhận popup.
jest.mock('./ProductExcelImports', () => ({
  ProductExcelImports: ({
    onChange,
    onProductRow,
  }: {
    onChange: (s: ProductImportSelection) => void;
    onProductRow: (r: ProductExcelRow) => void;
  }) => (
    <button
      onClick={() => {
        onChange({ bundle, fileName: 'all.xlsx' });
        onProductRow(row);
      }}
    >
      xác nhận popup
    </button>
  ),
}));

beforeEach(() => {
  mutate.mockReset();
  mockMissing.mockReset();
  mockMissing.mockReturnValue([]);
});

it('DEV-ALL-124: Thêm mới thiếu mục Excel bắt buộc thì không lưu', async () => {
  mockMissing.mockReturnValue(['markers', 'orderMaterials']);
  const onStage = jest.fn();
  render(
    <ProductFormModal open product={null} allowImports onStage={onStage} onClose={jest.fn()} />,
  );
  fireEvent.click(screen.getByText('xác nhận popup'));
  await waitFor(() => expect(screen.getByDisplayValue('TEST01')).toBeTruthy());

  fireEvent.click(screen.getByRole('button', { name: /Lưu/ }));

  await waitFor(() => expect(mockMissing).toHaveBeenCalled());
  expect(mutate).not.toHaveBeenCalled();
  expect(onStage).not.toHaveBeenCalled();
});

it('form đơn: Lưu không ghi mã hàng, giao dữ liệu đã sửa cho form đơn', async () => {
  const onStage = jest.fn();
  render(
    <ProductFormModal open product={null} allowImports onStage={onStage} onClose={jest.fn()} />,
  );
  fireEvent.click(screen.getByText('xác nhận popup'));
  await waitFor(() => expect(screen.getByDisplayValue('TEST01')).toBeTruthy());
  expect(screen.getByDisplayValue('Mô tả từ Excel')).toBeTruthy();

  fireEvent.click(screen.getByRole('button', { name: /Lưu/ }));

  await waitFor(() => expect(onStage).toHaveBeenCalled());
  expect(mutate).not.toHaveBeenCalled();
  expect(onStage.mock.calls[0][0]).toMatchObject({ productCode: 'TEST01' });
  expect(onStage.mock.calls[0][1]).toEqual({ bundle, fileName: 'all.xlsx' });
});
