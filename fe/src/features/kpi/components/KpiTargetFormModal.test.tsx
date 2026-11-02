import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import { kpiApi } from '../api/kpiApi';
import { KPI_TEXT } from '../constants';
import type { KpiOrderCapacity, KpiTarget } from '../types';
import { KpiTargetFormModal } from './KpiTargetFormModal';

jest.mock('../api/kpiApi', () => ({
  kpiApi: { getOrderCapacity: jest.fn(), getAllottedTeams: jest.fn().mockResolvedValue([]) },
}));
jest.mock('@/hooks/useOrderLookups', () => ({
  useOrderLookup: () => ({ data: { items: [] }, isLoading: false }),
  useOrderLineLookup: () => ({ data: [], isLoading: false }),
}));

const TARGET = {
  kpiTargetId: 1,
  orderId: 2,
  orderCode: 'DH002',
  productId: 3,
  productCode: 'MH003',
  productName: 'Quần',
  sewingTeamId: 4,
  sewingTeamName: 'Tổ may 1',
  startDate: '2030-01-05',
  endDate: '2030-01-18',
  targetFinishedQty: 100,
} as KpiTarget;

const CAPACITY: KpiOrderCapacity = {
  orderQty: 800,
  assignedToOthers: 300,
  maxTarget: 500,
  dueDate: '2030-01-20',
  dueWarningDays: 3,
  orderStatus: 'InProduction',
  orderClosed: false,
};

const renderForm = (capacity: KpiOrderCapacity, target = TARGET) => {
  (kpiApi.getOrderCapacity as jest.Mock).mockResolvedValue(capacity);
  render(
    <QueryClientProvider
      client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}
    >
      <KpiTargetFormModal open target={target} onClose={jest.fn()} />
    </QueryClientProvider>,
  );
};

describe('KpiTargetFormModal', () => {
  it('lấy danh sách tổ theo đơn + mã hàng đã chia, vẫn hiện tổ của chỉ tiêu đang sửa', async () => {
    renderForm({ ...CAPACITY, dueDate: '2030-02-20' });

    await waitFor(() => expect(kpiApi.getAllottedTeams).toHaveBeenCalledWith(2, 3));
    expect(await screen.findByText('Tổ may 1')).toBeInTheDocument();
  });

  it('hiện ngày giao hàng và số lượng đơn', async () => {
    renderForm({ ...CAPACITY, dueDate: '2030-02-20' });

    expect(await screen.findByText('20/02/2030')).toBeInTheDocument();
    expect(screen.getByText('800')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('cảnh báo khi kỳ kết thúc sát ngày giao, vẫn cho lưu', async () => {
    renderForm(CAPACITY);

    expect(
      await screen.findByText(/cách ngày giao hàng 20\/01\/2030 chỉ 2 ngày/),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Lưu/ })).toBeEnabled();
  });

  it('đơn đã dừng thì báo lỗi và khóa nút lưu', async () => {
    renderForm({ ...CAPACITY, orderStatus: 'Stopped', orderClosed: true });

    expect(await screen.findByText(KPI_TEXT.form.orderClosed)).toBeInTheDocument();
    expect(screen.getByText('Đã dừng')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Lưu/ })).toBeDisabled();
  });

  it('đơn đang giao thì cảnh báo nhưng vẫn cho lưu', async () => {
    renderForm({ ...CAPACITY, orderStatus: 'PartiallyDelivered' });

    expect(await screen.findByText(KPI_TEXT.form.orderDelivering)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Lưu/ })).toBeEnabled();
  });

  it('cảnh báo khi kỳ kết thúc sau ngày giao', async () => {
    renderForm({ ...CAPACITY, dueDate: '2030-01-15' });

    expect(
      await screen.findByText(new RegExp(KPI_TEXT.form.dueAfter.split('{0}')[0]!)),
    ).toHaveTextContent('3 ngày');
  });
});
