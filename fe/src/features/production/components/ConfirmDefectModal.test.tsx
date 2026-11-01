import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { PRODUCTION_TEXT } from '../constants';
import type { Defect } from '../types';
import { ConfirmDefectModal } from './ConfirmDefectModal';

const text = PRODUCTION_TEXT.defects;

const defect = {
  defectRecordId: 1,
  responsibleWorkerName: 'Nguyễn Văn A',
  workerObjection: 'Không phải lỗi của em',
} as Defect;

const renderModal = (onSubmit = jest.fn()) => {
  render(
    <ConfirmDefectModal defect={defect} loading={false} onCancel={jest.fn()} onSubmit={onSubmit} />,
  );
  return onSubmit;
};

const confirm = () => fireEvent.click(screen.getByRole('button', { name: text.confirm }));

describe('ConfirmDefectModal', () => {
  it('hiện nội dung phản đối của công nhân để Quản đốc đọc trước khi quyết định', () => {
    renderModal();

    expect(screen.getByText('Không phải lỗi của em')).toBeInTheDocument();
  });

  it('ghi chú không bắt buộc: chấp nhận không cần nhập gì', async () => {
    const onSubmit = renderModal();

    confirm();

    await waitFor(() => expect(onSubmit).toHaveBeenCalledWith(undefined));
  });

  it('gửi ghi chú đã cắt khoảng trắng hai đầu, ghi chú chỉ có khoảng trắng coi như không có', async () => {
    const onSubmit = renderModal();

    fireEvent.change(screen.getByLabelText(text.confirmNote), {
      target: { value: '  Đã kiểm tra, đúng lỗi  ' },
    });
    confirm();
    await waitFor(() => expect(onSubmit).toHaveBeenCalledWith('Đã kiểm tra, đúng lỗi'));

    fireEvent.change(screen.getByLabelText(text.confirmNote), { target: { value: '   ' } });
    confirm();
    await waitFor(() => expect(onSubmit).toHaveBeenLastCalledWith(undefined));
  });
});
