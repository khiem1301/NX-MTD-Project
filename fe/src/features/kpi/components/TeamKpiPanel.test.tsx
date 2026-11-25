import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { kpiApi } from '../api/kpiApi';
import { KPI_TEXT } from '../constants';
import type { KpiTarget } from '../types';
import { TeamKpiPanel } from './TeamKpiPanel';

jest.mock('../api/kpiApi', () => ({
  kpiApi: { getTeamTargets: jest.fn(), getTeamCalendar: jest.fn() },
}));

const getTeamTargets = kpiApi.getTeamTargets as jest.Mock;
const getTeamCalendar = kpiApi.getTeamCalendar as jest.Mock;

const TARGET: KpiTarget = {
  kpiTargetId: 7,
  orderId: 1,
  orderCode: 'DH001',
  productId: 2,
  productCode: 'MH001',
  sewingTeamId: 3,
  sewingTeamCode: 'Tổ may 1',
  startDate: '2026-10-14',
  endDate: '2026-10-17',
  targetFinishedQty: 100,
  workingDays: 4,
  dailyTarget: 25,
  status: 'Active',
  result: null,
  actualFinishedQty: 40,
  percentAchieved: 40,
  createdAt: '2026-10-13T08:00:00',
  lines: [
    { orderLineId: 11, color: 'Đen', sizeCode: 'M', targetQty: 60, actualQty: 30 },
    { orderLineId: 12, color: 'Đen', sizeCode: 'L', targetQty: 40, actualQty: 10 },
  ],
};

const renderPanel = () =>
  render(
    <QueryClientProvider
      client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}
    >
      <TeamKpiPanel />
    </QueryClientProvider>,
  );

describe('TeamKpiPanel', () => {
  beforeEach(() => {
    getTeamTargets.mockReset();
    getTeamCalendar.mockReset();
    getTeamCalendar.mockResolvedValue({
      kpiTargetId: 7,
      orderCode: 'DH001',
      productCode: 'MH001',
      sewingTeamCode: 'Tổ may 1',
      startDate: '2026-10-14',
      endDate: '2026-10-17',
      today: '2026-10-15',
      dueDate: '2026-10-20',
      firstEditableDate: '2026-10-16',
      editCutoff: '12:00',
      targetFinishedQty: 100,
      finishedQty: 40,
      canEdit: false,
      btp: { warehouseBtp: 0, teamHeld: 0, neededByOthers: 0, available: 0 },
      days: [],
    });
  });

  it('mặc định lấy chỉ tiêu đang theo dõi và hiện tiến độ, không có cột tổ may', async () => {
    getTeamTargets.mockResolvedValue({ items: [TARGET], totalItems: 1, page: 1, pageSize: 10 });
    renderPanel();

    expect(await screen.findByText('DH001')).toBeInTheDocument();
    expect(getTeamTargets).toHaveBeenCalledWith(expect.objectContaining({ status: 'Active' }));
    expect(screen.getByText(/40 \/ 100/)).toBeInTheDocument();
    expect(screen.queryByText(KPI_TEXT.table.sewingTeam)).not.toBeInTheDocument();
  });

  it('báo rõ khi tổ chưa được giao chỉ tiêu', async () => {
    getTeamTargets.mockResolvedValue({ items: [], totalItems: 0, page: 1, pageSize: 10 });
    renderPanel();

    await waitFor(() => expect(screen.getByText(KPI_TEXT.teamPanel.empty)).toBeInTheDocument());
  });

  it('bấm dòng đơn mở modal KPI theo ngày (chỉ xem)', async () => {
    getTeamTargets.mockResolvedValue({ items: [TARGET], totalItems: 1, page: 1, pageSize: 10 });
    const user = userEvent.setup();
    renderPanel();

    await user.click(await screen.findByText('DH001'));
    expect(await screen.findByText(/KPI theo ngày · DH001/)).toBeInTheDocument();
    expect(getTeamCalendar).toHaveBeenCalledWith(7);
  });
});
