import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { PURCHASE_TEXT } from '../constants';
import type { PurchaseOrderDetail } from '../types';
import { PayRemainingModal } from './PayRemainingModal';

const mutate = jest.fn();
jest.mock('../hooks/usePurchase', () => ({
  usePayRemaining: () => ({ mutate, isPending: false }),
}));

const text = PURCHASE_TEXT.payRemaining;
const order = {
  purchaseOrderId: 17,
  poCode: 'DM-1',
  paidAmount: 500_000,
  remainingAmount: 500_000,
  rowVer: 'AAA=',
} as PurchaseOrderDetail;

const submit = () =>
  fireEvent.click(screen.getByRole('button', { name: PURCHASE_TEXT.actions.payRemaining }));
const typeAmount = (value: string) =>
  fireEvent.change(screen.getByLabelText(text.amount), { target: { value } });

describe('PayRemainingModal', () => {
  beforeEach(() => mutate.mockReset());

  it('mặc định trả đủ phần còn lại và báo sẽ Đã thanh toán', async () => {
    render(<PayRemainingModal order={order} onClose={jest.fn()} />);
    expect(await screen.findByText(text.willBePaid)).toBeInTheDocument();

    submit();

    await waitFor(() => expect(mutate).toHaveBeenCalled());
    expect(mutate.mock.calls[0][0]).toMatchObject({ amount: 500_000, rowVer: 'AAA=' });
  });

  it('trả thiếu: báo vẫn Thanh toán một phần', async () => {
    render(<PayRemainingModal order={order} onClose={jest.fn()} />);
    typeAmount('200000');
    expect(await screen.findByText(text.willBePartial)).toBeInTheDocument();
  });

  it('trả vượt phần còn lại: báo lỗi, không gửi', async () => {
    render(<PayRemainingModal order={order} onClose={jest.fn()} />);
    typeAmount('600000');
    submit();

    expect(await screen.findByText(/Không được lớn hơn số còn phải trả/)).toBeInTheDocument();
    expect(mutate).not.toHaveBeenCalled();
  });
});
