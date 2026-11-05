import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import dayjs from 'dayjs';
import type { Operation } from '../types';
import { PieceRateFormModal } from './PieceRateFormModal';

const mockSave = jest.fn();
jest.mock('../hooks/usePieceRates', () => ({
  useSavePieceRate: () => ({ mutate: mockSave, isPending: false }),
}));

const operation = {
  operationId: 1,
  operationCode: 'OP01',
  operationName: 'May cổ',
  currentRate: null,
  nextRate: null,
} as Operation;
const rateBox = () => screen.getByRole('spinbutton');
const submitWith = (rate: string) => {
  fireEvent.change(rateBox(), { target: { value: rate } });
  fireEvent.blur(rateBox());
  fireEvent.click(screen.getByRole('button', { name: /Lưu/ }));
};

describe('PieceRateFormModal', () => {
  beforeEach(() => mockSave.mockClear());

  it('mặc định ngày hiệu lực là hôm nay, không cảnh báo ngày đã qua', () => {
    render(<PieceRateFormModal operation={operation} onClose={jest.fn()} />);

    expect(screen.getByRole('textbox')).toHaveValue(dayjs().format('DD/MM/YYYY'));
    expect(screen.queryByText(/Ngày hiệu lực đã qua/)).not.toBeInTheDocument();
  });

  it('bản Nháp ngày đã qua thì cảnh báo', async () => {
    render(
      <PieceRateFormModal
        operation={{
          ...operation,
          nextRate: {
            pieceRateId: 5,
            rate: 100,
            effectiveDate: dayjs().subtract(1, 'day').format('YYYY-MM-DD'),
            status: 'Draft',
            rejectReason: null,
          },
        }}
        onClose={jest.fn()}
      />,
    );

    expect(await screen.findByText(/Ngày hiệu lực đã qua/)).toBeInTheDocument();
  });

  it.each([
    ['0', /phải lớn hơn 0/],
    ['-5', /phải lớn hơn 0/],
    ['100000001', /không được vượt quá/],
  ])('chặn đơn giá %s', async (rate, message) => {
    render(<PieceRateFormModal operation={operation} onClose={jest.fn()} />);

    submitWith(rate);

    expect(await screen.findByText(message)).toBeInTheDocument();
    expect(mockSave).not.toHaveBeenCalled();
  });

  it.each([
    ['0.01', 0.01],
    ['100000000', 100000000],
  ])('nhận đơn giá ở biên %s', async (rate, expected) => {
    render(<PieceRateFormModal operation={operation} onClose={jest.fn()} />);

    submitWith(rate);

    await waitFor(() =>
      expect(mockSave).toHaveBeenCalledWith(
        expect.objectContaining({ operationId: 1, rate: expected }),
        expect.anything(),
      ),
    );
  });
});
