import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ExcelReviewModal } from './ExcelReviewModal';
import { excelBundleApi } from '../api/orderApi';
import type { ExcelBundle, ExcelBundleCheck } from '../types';

jest.mock('../api/orderApi', () => ({ excelBundleApi: { check: jest.fn() } }));
jest.mock('@/hooks/useLookups', () => ({
  useMaterialOptions: () => ({ data: [], isLoading: false }),
}));

const check = excelBundleApi.check as jest.Mock;

const bundle: ExcelBundle = {
  hasProducts: false,
  products: [],
  spec: {
    hasComponents: true,
    components: [
      { rowNumber: 5, name: 'Cổ', componentType: 'PANEL', piecesPerGarment: 1, parseError: null },
      { rowNumber: 6, name: '', componentType: 'PANEL', piecesPerGarment: 1, parseError: null },
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

/** Máy chủ: dòng tên trống là lỗi, còn lại hợp lệ. */
const respond = (b: ExcelBundle): ExcelBundleCheck =>
  ({
    products: null,
    orderMaterials: null,
    spec: {
      components: b.spec.components.map((c) => ({
        rowNumber: c.rowNumber,
        action: c.name ? 'New' : 'Error',
        error: c.name ? null : 'Thiếu tên chi tiết.',
        componentName: c.name,
        componentType: c.componentType,
        piecesPerGarment: c.piecesPerGarment,
      })),
      norms: [],
      markerGroups: [],
      orderMarkers: [],
      crossErrors: [],
      notes: [],
    },
  }) as unknown as ExcelBundleCheck;

const renderModal = (onConfirm = jest.fn()) =>
  render(
    <QueryClientProvider client={new QueryClient()}>
      <ExcelReviewModal
        fileName="all.xlsx"
        bundle={bundle}
        productId={null}
        orderId={null}
        onConfirm={onConfirm}
        onCancel={jest.fn()}
      />
    </QueryClientProvider>,
  );

beforeEach(() => {
  jest.useFakeTimers();
  check.mockReset();
  check.mockImplementation(({ bundle: b }: { bundle: ExcelBundle }) => Promise.resolve(respond(b)));
});
afterEach(() => jest.useRealTimers());

it('hiện dòng lỗi; sửa ô thì kiểm lại; Xác nhận trả bản đã sửa', async () => {
  const onConfirm = jest.fn();
  renderModal(onConfirm);
  await act(async () => {
    jest.advanceTimersByTime(600);
  });
  await waitFor(() => expect(screen.getByText('Thiếu tên chi tiết.')).toBeTruthy());

  const inputs = screen.getAllByRole('textbox');
  fireEvent.change(inputs[1] as Element, { target: { value: 'Thân' } });
  await act(async () => {
    jest.advanceTimersByTime(600);
  });

  await waitFor(() => expect(screen.queryByText('Thiếu tên chi tiết.')).toBeNull());
  expect(check).toHaveBeenCalledTimes(2);

  fireEvent.click(screen.getByRole('button', { name: /Xác nhận/ }));
  const confirmed = onConfirm.mock.calls[0][0] as ExcelBundle;
  expect(confirmed.spec.components.map((c) => c.name)).toEqual(['Cổ', 'Thân']);
});

describe('NVL chưa có trong danh mục (DEV-ALL-123)', () => {
  beforeEach(() =>
    check.mockImplementation(({ bundle: b }: { bundle: ExcelBundle }) =>
      Promise.resolve({
        ...respond(b),
        missingMaterials: [{ materialCode: 'V-9999', suggestedType: 'FABRIC', colors: ['Hồng'] }],
      }),
    ),
  );

  const openAsk = async (materialName?: string) => {
    const onConfirm = jest.fn();
    renderModal(onConfirm);
    await act(async () => {
      jest.advanceTimersByTime(600);
    });
    await waitFor(() => expect(screen.getByText(/1 NVL chưa có trong danh mục kho/)).toBeTruthy());
    if (materialName)
      fireEvent.change(screen.getByPlaceholderText('Tên NVL'), { target: { value: materialName } });
    fireEvent.click(screen.getByRole('button', { name: /Xác nhận/ }));
    await waitFor(() =>
      expect(screen.getAllByText('Thêm NVL vào danh mục?').length).toBeGreaterThan(0),
    );
    return onConfirm;
  };

  it('Không thêm: vẫn xác nhận, không kèm NVL mới', async () => {
    const onConfirm = await openAsk();
    fireEvent.click(screen.getByRole('button', { name: 'Không thêm' }));
    await waitFor(() => expect(onConfirm).toHaveBeenCalled());
    expect(onConfirm.mock.calls[0][1]).toEqual([]);
  });

  it('Nút X: đóng hộp hỏi, quay lại popup, chưa xác nhận gì', async () => {
    const onConfirm = await openAsk();
    const closeButtons = screen.getAllByRole('button', { name: /close|đóng/i });
    fireEvent.click(closeButtons[closeButtons.length - 1] as Element);
    await act(async () => {
      jest.advanceTimersByTime(500);
    });
    expect(onConfirm).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: /Xác nhận/ })).toBeTruthy();
  });

  it('Thêm vào danh mục mà thiếu tên NVL thì chưa xác nhận', async () => {
    const onConfirm = await openAsk();
    fireEvent.click(screen.getByRole('button', { name: 'Thêm vào danh mục' }));
    await act(async () => {
      jest.advanceTimersByTime(100);
    });
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it('Thêm vào danh mục: kèm NVL mới đã điền (gợi ý vải, màu, khổ)', async () => {
    const onConfirm = await openAsk('Lụa hồng mới');
    fireEvent.click(screen.getByRole('button', { name: 'Thêm vào danh mục' }));

    await waitFor(() => expect(onConfirm).toHaveBeenCalled());
    expect(onConfirm.mock.calls[0][1]).toEqual([
      expect.objectContaining({
        materialCode: 'V-9999',
        materialName: 'Lụa hồng mới',
        materialType: 'FABRIC',
        color: 'Hồng',
        widthM: 1.5,
      }),
    ]);
  });
});

it('Thêm dòng và xóa dòng', async () => {
  const onConfirm = jest.fn();
  renderModal(onConfirm);
  fireEvent.click(screen.getByRole('button', { name: /Thêm dòng/ }));
  fireEvent.click(screen.getAllByRole('button', { name: /Xóa/ })[0] as Element);
  fireEvent.click(screen.getByRole('button', { name: /Xác nhận/ }));

  const confirmed = onConfirm.mock.calls[0][0] as ExcelBundle;
  expect(confirmed.spec.components.map((c) => c.rowNumber)).toEqual([6, 7]);
});
