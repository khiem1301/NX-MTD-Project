import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { useWorkerHome } from '../../hooks/useHome';
import type { WorkerHome as WorkerHomeData } from '../../types';
import { WorkerHome } from './WorkerHome';

jest.mock('../../hooks/useHome', () => ({ useWorkerHome: jest.fn() }));

const base: WorkerHomeData = {
  estimatedToday: 186000,
  estimatedMonth: 3420000,
  latestPayslip: { periodMonth: '2026-09', netPay: 4850000 },
  allocationsPending: 1,
  allocations: [{ title: 'May cổ · 80 sp', meta: 'DH-5', tag: 'Nhận', tagColor: 'blue' }],
  defects: [],
  mealHasAttendance: true,
  mealWillEat: true,
  mealLocked: false,
};

const renderWith = (data: WorkerHomeData) => {
  (useWorkerHome as jest.Mock).mockReturnValue({ isPending: false, isError: false, data });
  render(
    <MemoryRouter>
      <WorkerHome />
    </MemoryRouter>,
  );
};

describe('WorkerHome', () => {
  it('khoán sản phẩm: hiện lương tạm tính (ghi rõ là số tạm tính) và việc chờ xác nhận', () => {
    renderWith(base);

    expect(screen.getByText('Tạm tính hôm nay')).toBeInTheDocument();
    expect(screen.getByText(/Số tạm tính, không phải lương chính thức/)).toBeInTheDocument();
    expect(screen.getByText('May cổ · 80 sp')).toBeInTheDocument();
    expect(screen.getByText('Có ăn')).toBeInTheDocument();
  });

  it('không khoán sản phẩm: ẩn lương tạm tính', () => {
    renderWith({ ...base, estimatedToday: null, estimatedMonth: null });

    expect(screen.queryByText('Tạm tính hôm nay')).not.toBeInTheDocument();
    expect(screen.getByText('Phiếu lương mới nhất')).toBeInTheDocument();
  });

  it('chưa quẹt vân tay thì suất ăn báo chưa có giờ vào', () => {
    renderWith({ ...base, mealHasAttendance: false });

    expect(screen.getByText('Chưa có giờ vào hôm nay')).toBeInTheDocument();
  });
});
