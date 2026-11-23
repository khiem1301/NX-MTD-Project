import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { useBreakEvenInputs, useCalculateBreakEven } from '../hooks/useBreakEven';
import type { BreakEvenInputs } from '../types';
import { BreakEvenCalculateModal } from './BreakEvenCalculateModal';

jest.mock('../hooks/useBreakEven', () => ({
  useBreakEvenInputs: jest.fn(),
  useCalculateBreakEven: jest.fn(),
}));

const mutate = jest.fn();
const ready: BreakEvenInputs = {
  periodExists: true,
  periodLocked: false,
  payrollApproved: true,
  costEntered: true,
  fixedTotal: 40000000,
  variableTotal: 96000000,
  variableProductCount: 3,
  productCount: 3,
  finishedQty: 1240,
  revenue: 182400000,
  labourHours: 2310,
  canCalculate: true,
};

const renderWith = (data: BreakEvenInputs) => {
  (useBreakEvenInputs as jest.Mock).mockReturnValue({ isPending: false, isError: false, data });
  (useCalculateBreakEven as jest.Mock).mockReturnValue({ mutate, isPending: false });
  render(
    <MemoryRouter>
      <BreakEvenCalculateModal periodMonth="2026-10" open onClose={jest.fn()} />
    </MemoryRouter>,
  );
};

const submit = () => screen.getByRole('button', { name: /Tính ngay/ });

describe('BreakEvenCalculateModal', () => {
  beforeEach(() => mutate.mockReset());

  it('đủ dữ liệu: hiện tổng đầu vào, bấm Tính ngay thì tính', () => {
    renderWith(ready);

    expect(screen.getByText('Đã duyệt')).toBeInTheDocument();
    expect(screen.getByText(/3 mã hàng · 1.240 sp/)).toBeInTheDocument();
    expect(screen.getByText(/2.310 giờ/)).toBeInTheDocument();
    fireEvent.click(submit());
    expect(mutate).toHaveBeenCalled();
  });

  it('bảng lương chưa duyệt: khóa nút Tính và chỉ đường tới màn duyệt lương', () => {
    renderWith({ ...ready, payrollApproved: false, canCalculate: false });

    expect(submit()).toBeDisabled();
    expect(screen.getByRole('link', { name: /Đến màn duyệt bảng lương/ })).toBeInTheDocument();
  });

  it('tháng đã khóa sổ: báo kết quả chỉ để tham khảo nhưng vẫn cho tính', () => {
    renderWith({ ...ready, periodLocked: true });

    expect(screen.getByText(/Tháng đã khóa sổ/)).toBeInTheDocument();
    expect(submit()).toBeEnabled();
  });

  it('thiếu giờ công chỉ cảnh báo, vẫn tính được', () => {
    renderWith({ ...ready, labourHours: 0 });

    expect(screen.getByText(/cách giờ công sẽ chia theo sản lượng/)).toBeInTheDocument();
    expect(submit()).toBeEnabled();
  });
});
