import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { message } from 'antd';
import { CopyModal } from './CopyModal';

const mockCopy = jest.fn();
let mockPick = 9;

jest.mock('../hooks/useProduction', () => ({
  useCopyAssignments: () => ({ mutate: mockCopy, isPending: false }),
}));
jest.mock('@/components/common/OrderSelect', () => ({
  OrderSelect: ({ onChange }: { onChange?: (id: number) => void }) => (
    <button type="button" onClick={() => onChange?.(mockPick)}>
      chọn đơn nguồn
    </button>
  ),
}));

const submit = () => {
  render(<CopyModal open toOrderId={5} onClose={jest.fn()} />);
  fireEvent.click(screen.getByText('chọn đơn nguồn'));
  fireEvent.click(document.querySelector('.ant-btn-primary') as Element);
};

describe('CopyModal', () => {
  beforeEach(() => {
    mockCopy.mockClear();
    mockPick = 9;
  });

  it('chọn đơn nguồn trùng đơn đang chọn thì báo lỗi, không gọi API', async () => {
    mockPick = 5;
    submit();

    expect(await screen.findByText('Đơn nguồn phải khác đơn đang chọn.')).toBeInTheDocument();
    expect(mockCopy).not.toHaveBeenCalled();
  });

  it('không có gì để chép thì báo rõ thay vì báo đã chép', async () => {
    const info = jest.spyOn(message, 'info').mockImplementation(jest.fn());
    submit();
    await waitFor(() => expect(mockCopy).toHaveBeenCalled());

    mockCopy.mock.calls[0][1].onSuccess(0);

    expect(info).toHaveBeenCalledWith('Không có phân công nào để chép từ đơn này.');
    info.mockRestore();
  });

  it('chép được thì báo số phân công', async () => {
    const success = jest.spyOn(message, 'success').mockImplementation(jest.fn());
    submit();
    await waitFor(() => expect(mockCopy).toHaveBeenCalled());

    mockCopy.mock.calls[0][1].onSuccess(3);

    expect(success).toHaveBeenCalledWith('Đã chép 3 phân công.');
    success.mockRestore();
  });
});
