import { fireEvent, render, screen } from '@testing-library/react';
import { DetailModal } from '@/components/common/DetailModal';
import type { ProductListItem } from '../types';
import { SpecEditorActions } from './SpecEditorActions';

jest.mock('./SpecImportModal', () => ({
  SpecImportModal: ({ source }: { source: { productLabel: string } }) => (
    <div>hộp thoại nhập {source.productLabel}</div>
  ),
}));

const product = {
  productId: 1,
  productCode: 'AO01',
  productName: 'Áo khoác',
} as ProductListItem;

/** Nút nằm ở chân DetailModal (ModalActions đưa nút vào đó), nên bọc trong DetailModal như form định mức thật. */
const renderActions = (readOnly: boolean, dirty = false) =>
  render(
    <DetailModal open title="Định mức" onClose={jest.fn()}>
      <SpecEditorActions
        product={product}
        readOnly={readOnly}
        dirty={dirty}
        saving={false}
        onSave={jest.fn()}
        onClose={jest.fn()}
      />
    </DetailModal>,
  );

describe('SpecEditorActions', () => {
  it('Kế toán (chỉ xem, DEV-ALL-116) không có nút Nhập từ Excel và Lưu', () => {
    renderActions(true);

    expect(screen.getByRole('button', { name: 'Đóng' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Nhập từ Excel/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Lưu' })).not.toBeInTheDocument();
  });

  it('Admin mở hộp thoại nhập Excel của mã hàng', () => {
    renderActions(false);

    fireEvent.click(screen.getByRole('button', { name: /Nhập từ Excel/ }));

    expect(screen.getByText('hộp thoại nhập AO01 - Áo khoác')).toBeInTheDocument();
  });

  it('form còn thay đổi chưa lưu thì hỏi trước khi mở', async () => {
    renderActions(false, true);

    fireEvent.click(screen.getByRole('button', { name: /Nhập từ Excel/ }));

    expect(await screen.findByText(/Định mức đang có thay đổi chưa lưu/)).toBeInTheDocument();
    expect(screen.queryByText(/hộp thoại nhập/)).not.toBeInTheDocument();
  });
});
