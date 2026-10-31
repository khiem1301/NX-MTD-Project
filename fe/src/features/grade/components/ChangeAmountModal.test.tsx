import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { gradeApi } from '../api/gradeApi';
import { GR_LABELS } from '../constants';
import type { GradeCode, GradeLevel } from '../types';
import { ChangeAmountModal } from './ChangeAmountModal';

jest.mock('../api/gradeApi', () => ({ gradeApi: { addLevel: jest.fn() } }));

const CODE: GradeCode = { gradeCode: 'D', gradeName: 'Bậc D – Yếu', status: 'Active', inUse: true };
const ROWS: GradeLevel[] = [
  {
    gradeLevelId: 1,
    gradeCode: 'D',
    monthlyAmount: 0,
    effectiveDate: '2026-01-01',
    isCurrent: true,
  },
];

const renderModal = () =>
  render(
    <QueryClientProvider
      client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}
    >
      <ChangeAmountModal code={CODE} rows={ROWS} onClose={jest.fn()} />
    </QueryClientProvider>,
  );

const amountInput = () => document.querySelector('.ant-input-number-input') as HTMLInputElement;

describe('ChangeAmountModal', () => {
  beforeEach(() => (gradeApi.addLevel as jest.Mock).mockReset());

  it('nhập mức khác mức hiện tại thì không báo trùng và gửi đúng số tiền', async () => {
    (gradeApi.addLevel as jest.Mock).mockResolvedValue({});
    renderModal();

    fireEvent.change(amountInput(), { target: { value: '2.000' } });
    fireEvent.click(screen.getByRole('button', { name: /Lưu/ }));

    await waitFor(() => expect(gradeApi.addLevel).toHaveBeenCalled());
    expect(screen.queryByText(GR_LABELS.sameAmount)).toBeNull();
    expect((gradeApi.addLevel as jest.Mock).mock.calls[0][0]).toMatchObject({
      gradeCode: 'D',
      monthlyAmount: 2000,
    });
  });

  it('nhập đúng mức hiện tại thì báo trùng và không gọi API', async () => {
    renderModal();

    fireEvent.change(amountInput(), { target: { value: '0' } });
    fireEvent.click(screen.getByRole('button', { name: /Lưu/ }));

    expect(await screen.findByText(GR_LABELS.sameAmount)).toBeInTheDocument();
    expect(gradeApi.addLevel).not.toHaveBeenCalled();
  });
});
