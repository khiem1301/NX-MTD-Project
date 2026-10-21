import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { NoteModal } from './NoteModal';

const renderModal = (required: boolean, onSubmit = jest.fn()) => {
  render(
    <NoteModal open title="Hủy đơn" label="Ghi chú" required={required} onSubmit={onSubmit} onCancel={jest.fn()} />,
  );
  return onSubmit;
};

describe('NoteModal', () => {
  it('bắt buộc nhập: để trống thì báo MSG09 và không gửi', async () => {
    const onSubmit = renderModal(true);

    fireEvent.click(screen.getByRole('button', { name: /Xác nhận/ }));

    expect(await screen.findByText('Vui lòng nhập lý do.')).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('bắt buộc nhập: có nội dung thì gửi bản đã cắt khoảng trắng', async () => {
    const onSubmit = renderModal(true);

    fireEvent.change(screen.getByLabelText('Ghi chú'), { target: { value: ' abc ' } });
    fireEvent.click(screen.getByRole('button', { name: /Xác nhận/ }));

    await waitFor(() => expect(onSubmit).toHaveBeenCalledWith('abc'));
  });

  it('không bắt buộc: để trống vẫn gửi null', async () => {
    const onSubmit = renderModal(false);

    fireEvent.click(screen.getByRole('button', { name: /Xác nhận/ }));

    await waitFor(() => expect(onSubmit).toHaveBeenCalledWith(null));
  });
});
