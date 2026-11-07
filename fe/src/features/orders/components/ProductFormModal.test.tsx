import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import type { ProductListItem } from '../types';
import { ProductFormModal } from './ProductFormModal';

const mutate = jest.fn();
let detailData: { description: string | null } | undefined;

jest.mock('../hooks/useOrders', () => ({
  useSaveProduct: () => ({ mutate, isPending: false }),
  useProduct: () => ({ data: detailData, error: null, refetch: jest.fn() }),
}));

// Ô ảnh kéo theo cấu hình môi trường (import.meta) mà jest không đọc được; test này không cần ảnh.
jest.mock('./SampleImageInput', () => ({ SampleImageInput: () => null }));
jest.mock('./ExtraImagesInput', () => ({ ExtraImagesInput: () => null }));
// Ô màu gọi API gợi ý màu (cần QueryClient); thay bằng ô nhập thường.
jest.mock('./ColorAutoComplete', () => ({
  ColorAutoComplete: ({
    value,
    onChange,
    placeholder,
  }: {
    value?: string;
    onChange?: (v: string) => void;
    placeholder?: string;
  }) => (
    <input
      placeholder={placeholder}
      value={value ?? ''}
      onChange={(e) => onChange?.(e.target.value)}
    />
  ),
}));

const product: ProductListItem = {
  productId: 7,
  productCode: 'AO-01',
  productName: 'Áo khoác',
  sampleImageUrl: null,
  extraImages: [],
  status: 'Active',
  isUsed: false,
  createdAt: '2026-10-01T00:00:00',
};

const renderModal = (item: ProductListItem | null) =>
  render(<ProductFormModal open product={item} onClose={jest.fn()} />);

const field = (label: string) => screen.getByLabelText(label) as HTMLInputElement;
const clickSave = () => fireEvent.click(screen.getByRole('button', { name: 'Lưu' }));

describe('ProductFormModal', () => {
  beforeEach(() => {
    mutate.mockClear();
    detailData = undefined;
  });

  it('sửa mã hàng: nạp mô tả từ chi tiết và gửi lại khi lưu, không làm mất mô tả cũ', async () => {
    detailData = { description: 'Mô tả cũ' };
    renderModal(product);

    await waitFor(() => expect(field('Mô tả').value).toBe('Mô tả cũ'));
    clickSave();

    await waitFor(() => expect(mutate).toHaveBeenCalledTimes(1));
    expect(mutate.mock.calls[0][0].body).toMatchObject({
      productCode: 'AO-01',
      description: 'Mô tả cũ',
    });
  });

  it('sửa mã hàng: ảnh phụ đang có được gửi lại khi lưu, không bị mất', async () => {
    const extra = ['PRODUCT/2026/11/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.jpg'];
    detailData = { description: null };
    renderModal({ ...product, extraImages: extra });

    clickSave();

    await waitFor(() => expect(mutate).toHaveBeenCalledTimes(1));
    expect(mutate.mock.calls[0][0].body.extraImages).toEqual(extra);
  });

  it('chi tiết chưa tải xong thì nút Lưu bị khóa để không ghi đè mô tả bằng giá trị trống', () => {
    renderModal(product);

    expect(screen.getByRole('button', { name: 'Lưu' })).toBeDisabled();
  });

  it('tạo mới thì không cần chờ chi tiết', () => {
    renderModal(null);

    expect(screen.getByRole('button', { name: 'Lưu' })).toBeEnabled();
  });

  it.each([
    ['AO 01', 'Mã hàng chỉ gồm chữ không dấu, số và . _ -'],
    ['A'.repeat(31), 'Mã hàng tối đa 30 ký tự.'],
  ])('mã "%s" sai thì báo lỗi ngay tại ô và không gửi', async (code, message) => {
    renderModal(null);

    fireEvent.change(field('Mã hàng'), { target: { value: code } });
    fireEvent.change(field('Tên mã hàng'), { target: { value: 'Áo' } });
    clickSave();

    expect(await screen.findByText(message)).toBeInTheDocument();
    expect(mutate).not.toHaveBeenCalled();
  });

  it('tên mã hàng chỉ có khoảng trắng thì báo bắt buộc', async () => {
    renderModal(null);

    fireEvent.change(field('Mã hàng'), { target: { value: 'AO-02' } });
    fireEvent.change(field('Tên mã hàng'), { target: { value: '   ' } });
    clickSave();

    expect((await screen.findAllByText('Vui lòng nhập thông tin này.')).length).toBeGreaterThan(0);
    expect(mutate).not.toHaveBeenCalled();
  });
});
