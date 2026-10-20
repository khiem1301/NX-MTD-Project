import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { ReasonModal } from './ReasonModal';

const renderModal = (onSubmit = jest.fn()) => {
  render(<ReasonModal open title="Từ chối phiếu" onSubmit={onSubmit} onCancel={jest.fn()} />);
  return onSubmit;
};

describe('ReasonModal', () => {
  it('báo lỗi và không gửi khi lý do để trống', async () => {
    const onSubmit = renderModal();

    fireEvent.click(screen.getByRole('button', { name: /Xác nhận/ }));

    expect(await screen.findByText('Vui lòng nhập lý do.')).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('không chấp nhận lý do chỉ có khoảng trắng', async () => {
    const onSubmit = renderModal();

    fireEvent.change(screen.getByLabelText('Lý do'), { target: { value: '   ' } });
    fireEvent.click(screen.getByRole('button', { name: /Xác nhận/ }));

    expect(await screen.findByText('Vui lòng nhập lý do.')).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('gửi lý do đã cắt khoảng trắng hai đầu', async () => {
    const onSubmit = renderModal();

    fireEvent.change(screen.getByLabelText('Lý do'), { target: { value: '  Sai số lượng  ' } });
    fireEvent.click(screen.getByRole('button', { name: /Xác nhận/ }));

    await waitFor(() => expect(onSubmit).toHaveBeenCalledWith('Sai số lượng'));
  });
});
