import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ExcelReviewModal } from './ExcelReviewModal';
import { excelBundleApi } from '../api/orderApi';
import type { ExcelBundle, ExcelBundleCheck } from '../types';

jest.mock('../api/orderApi', () => ({ excelBundleApi: { check: jest.fn() } }));
// Nạp dữ liệu đang có gọi API thật: tắt trong test này.
jest.mock('../hooks/useExistingData', () => ({
  useExistingData: () => ({ load: jest.fn(), loading: false, available: false }),
  useReviewDbOptions: () => ({ components: [], colors: [] }),
}));
// Mở lại NVL ngừng sử dụng gọi API thật: tắt trong test này.
jest.mock('@/services/materialApi', () => ({ materialApi: { reopen: jest.fn() } }));
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
  hasMaterials: false,
  materials: [],
};

/** Bản không có dòng lỗi (Xác nhận được). */
const validBundle: ExcelBundle = {
  ...bundle,
  spec: {
    ...bundle.spec,
    components: bundle.spec.components.map((c) => ({ ...c, name: c.name || 'Thân' })),
  },
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

const renderModal = (onConfirm = jest.fn(), b: ExcelBundle = bundle) =>
  render(
    <QueryClientProvider client={new QueryClient()}>
      <ExcelReviewModal
        fileName="all.xlsx"
        bundle={b}
        productId={null}
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
  // Lý do lỗi không hiện sẵn: trỏ chuột vào nhãn "Lỗi" mới thấy.
  await waitFor(() => expect(screen.getByText('Lỗi')).toBeTruthy());
  fireEvent.mouseEnter(screen.getByRole('img', { name: 'info-circle' }));
  await waitFor(() => expect(screen.getByText('Thiếu tên chi tiết.')).toBeTruthy());

  const inputs = screen.getAllByRole('textbox');
  fireEvent.change(inputs[1] as Element, { target: { value: 'Thân' } });
  await act(async () => {
    jest.advanceTimersByTime(600);
  });

  await waitFor(() => expect(screen.queryByRole('img', { name: 'info-circle' })).toBeNull());
  expect(check).toHaveBeenCalledTimes(2);

  fireEvent.click(screen.getByRole('button', { name: /Xác nhận/ }));
  const confirmed = onConfirm.mock.calls[0][0] as ExcelBundle;
  expect(confirmed.spec.components.map((c) => c.name)).toEqual(['Cổ', 'Thân']);
  expect(onConfirm.mock.calls[0][2]).toBe(false);
});

it('DEV-ALL-155: form sửa mã hàng hiện ô thay thế; bật thì Xác nhận gửi replace=true', async () => {
  const onConfirm = jest.fn();
  check.mockImplementation(({ bundle: b, replace }: { bundle: ExcelBundle; replace?: boolean }) =>
    Promise.resolve({
      ...respond(b),
      spec: {
        ...respond(b).spec,
        removedNames: replace ? ['Thành phần: Cổ áo cũ'] : null,
        removedCount: replace ? 1 : 0,
      },
    }),
  );
  render(
    <QueryClientProvider client={new QueryClient()}>
      <ExcelReviewModal
        fileName="all.xlsx"
        bundle={validBundle}
        productId={7}
        allowReplace
        onConfirm={onConfirm}
        onCancel={jest.fn()}
      />
    </QueryClientProvider>,
  );
  expect(screen.getByText(/Thay thế dữ liệu cũ/)).toBeTruthy();
  fireEvent.click(screen.getByRole('checkbox', { name: /Thay thế dữ liệu cũ/ }));
  await act(async () => {
    jest.advanceTimersByTime(600);
  });
  await waitFor(() => expect(screen.getByText(/Sẽ xóa 1 dòng/)).toBeTruthy());
  fireEvent.click(screen.getByRole('button', { name: /Xác nhận/ }));
  expect(onConfirm.mock.calls[0][2]).toBe(true);
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

  const confirmWith = async (materialName?: string) => {
    const onConfirm = jest.fn();
    renderModal(onConfirm, validBundle);
    await act(async () => {
      jest.advanceTimersByTime(600);
    });
    await waitFor(() => expect(screen.getByText(/1 NVL chưa có trong danh mục kho/)).toBeTruthy());
    if (materialName)
      fireEvent.change(screen.getByPlaceholderText('Tên NVL'), { target: { value: materialName } });
    fireEvent.click(screen.getByRole('button', { name: /Xác nhận/ }));
    await act(async () => {
      jest.advanceTimersByTime(100);
    });
    return onConfirm;
  };

  it('Xác nhận không hiện hộp hỏi thêm NVL', async () => {
    await confirmWith('Lụa hồng mới');
    expect(screen.queryByText('Thêm NVL vào danh mục?')).toBeNull();
  });

  it('Xác nhận mà thiếu tên NVL thì chưa xác nhận', async () => {
    const onConfirm = await confirmWith();
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it('Xác nhận: tự thêm NVL mới đã điền (gợi ý vải, màu, khổ)', async () => {
    const onConfirm = await confirmWith('Lụa hồng mới');

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
  renderModal(onConfirm, validBundle);
  fireEvent.click(screen.getByRole('button', { name: /Thêm dòng/ }));
  // Dòng mới còn trống tên là lỗi thiếu trường: xóa đi mới Xác nhận được.
  const deletes = screen.getAllByRole('button', { name: /Xóa/ });
  fireEvent.click(deletes[deletes.length - 1] as Element);
  fireEvent.click(screen.getAllByRole('button', { name: /Xóa/ })[0] as Element);
  fireEvent.click(screen.getByRole('button', { name: /Xác nhận/ }));

  const confirmed = onConfirm.mock.calls[0][0] as ExcelBundle;
  expect(confirmed.spec.components.map((c) => c.rowNumber)).toEqual([6]);
});

it('Còn dòng lỗi thì không cho Xác nhận', async () => {
  const onConfirm = jest.fn();
  renderModal(onConfirm);
  await act(async () => {
    jest.advanceTimersByTime(600);
  });
  fireEvent.click(screen.getByRole('button', { name: /Xác nhận/ }));
  expect(onConfirm).not.toHaveBeenCalled();
});
