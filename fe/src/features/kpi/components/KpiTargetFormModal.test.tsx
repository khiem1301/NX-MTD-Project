import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { kpiApi } from '../api/kpiApi';
import { KPI_TEXT } from '../constants';
import type { KpiLineCapacity, KpiOrderCapacity, KpiTarget } from '../types';
import { KpiTargetFormModal } from './KpiTargetFormModal';

jest.mock('../api/kpiApi', () => ({
  kpiApi: {
    getOrderCapacity: jest.fn(),
    getAllottedTeams: jest.fn().mockResolvedValue([]),
    getLineCapacity: jest.fn(),
  },
}));

const LINES: KpiLineCapacity[] = [
  {
    orderLineId: 11,
    color: 'Đen',
    sizeCode: 'M',
    orderedQty: 300,
    assignedToOthers: 100,
    maxTarget: 200,
    actualQty: 0,
  },
  {
    orderLineId: 12,
    color: 'Trắng',
    sizeCode: 'L',
    orderedQty: 500,
    assignedToOthers: 200,
    maxTarget: 300,
    actualQty: 20,
  },
];
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
  lines: [
    { orderLineId: 11, color: 'Đen', sizeCode: 'M', targetQty: 60, actualQty: 0 },
    { orderLineId: 12, color: 'Trắng', sizeCode: 'L', targetQty: 40, actualQty: 20 },
  ],
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

const renderForm = (capacity: KpiOrderCapacity, target = TARGET, lines = LINES) => {
  (kpiApi.getOrderCapacity as jest.Mock).mockResolvedValue(capacity);
  (kpiApi.getLineCapacity as jest.Mock).mockResolvedValue(lines);
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

  it('nhập theo màu × size của tổ, tổng cả kỳ là tổng các dòng', async () => {
    renderForm({ ...CAPACITY, dueDate: '2030-02-20' });

    await waitFor(() =>
      expect(kpiApi.getLineCapacity).toHaveBeenCalledWith({
        orderId: 2,
        productId: 3,
        sewingTeamId: 4,
        exceptKpiTargetId: 1,
      }),
    );
    expect(await screen.findByLabelText('Chỉ tiêu màu Đen, size M')).toHaveValue('60');
    expect(screen.getByLabelText('Chỉ tiêu màu Trắng, size L')).toHaveValue('40');
    expect(screen.getAllByText(KPI_TEXT.form.lineActual).length).toBeGreaterThan(0);
    expect(screen.getByText(`${KPI_TEXT.form.linesTotal}:`, { exact: false })).toHaveTextContent(
      '100',
    );
    expect(screen.getByRole('button', { name: /Lưu/ })).toBeEnabled();
  });

  it('một màu × size vượt số còn lại thì khóa nút lưu', async () => {
    renderForm({ ...CAPACITY, dueDate: '2030-02-20' });
    const input = await screen.findByLabelText('Chỉ tiêu màu Đen, size M');

    fireEvent.change(input, { target: { value: '201' } });

    await waitFor(() => expect(screen.getByRole('button', { name: /Lưu/ })).toBeDisabled());
    fireEvent.change(input, { target: { value: '200' } });
    await waitFor(() => expect(screen.getByRole('button', { name: /Lưu/ })).toBeEnabled());
  });

  it('chỉ tiêu cũ chỉ có số tổng thì nhắc nhập lại theo màu × size, chưa cho lưu', async () => {
    renderForm({ ...CAPACITY, dueDate: '2030-02-20' }, { ...TARGET, lines: [] });

    expect(
      await screen.findByText(/lập trước khi chia theo màu × size \(tổng 100\)/),
    ).toBeInTheDocument();
    await screen.findByLabelText('Chỉ tiêu màu Đen, size M');
    expect(screen.getByRole('button', { name: /Lưu/ })).toBeDisabled();
  });

  it('cảnh báo khi kỳ kết thúc sau ngày giao', async () => {
    renderForm({ ...CAPACITY, dueDate: '2030-01-15' });

    expect(
      await screen.findByText(new RegExp(KPI_TEXT.form.dueAfter.split('{0}')[0]!)),
    ).toHaveTextContent('3 ngày');
  });
});
