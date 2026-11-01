import { fireEvent, render, screen } from '@testing-library/react';
import { UnsavedChangesModal } from './UnsavedChangesModal';

const renderModal = (open = true) => {
  const onLeave = jest.fn();
  const onStay = jest.fn();
  render(<UnsavedChangesModal open={open} onLeave={onLeave} onStay={onStay} />);
  return { onLeave, onStay };
};

describe('UnsavedChangesModal', () => {
  it('hiện lời cảnh báo và hai lựa chọn tiếng Việt', () => {
    renderModal();
    expect(screen.getByText('Có thay đổi chưa lưu')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Rời trang' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Ở lại' })).toBeInTheDocument();
  });

  it('bấm Rời trang thì rời, không gọi Ở lại', () => {
    const { onLeave, onStay } = renderModal();
    fireEvent.click(screen.getByRole('button', { name: 'Rời trang' }));
    expect(onLeave).toHaveBeenCalledTimes(1);
    expect(onStay).not.toHaveBeenCalled();
  });

  it('bấm Ở lại hoặc nút đóng thì ở lại trang', () => {
    const { onLeave, onStay } = renderModal();
    fireEvent.click(screen.getByRole('button', { name: 'Ở lại' }));
    fireEvent.click(screen.getByRole('button', { name: 'Close' }));
    expect(onStay).toHaveBeenCalledTimes(2);
    expect(onLeave).not.toHaveBeenCalled();
  });

  it('đóng thì không hiện gì', () => {
    renderModal(false);
    expect(screen.queryByText('Có thay đổi chưa lưu')).not.toBeInTheDocument();
  });
});
