import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import { kpiApi } from '../api/kpiApi';
import { KPI_TEXT } from '../constants';
import type { KpiTarget } from '../types';
import { TeamKpiPanel } from './TeamKpiPanel';

jest.mock('../api/kpiApi', () => ({ kpiApi: { getTeamTargets: jest.fn() } }));

const getTeamTargets = kpiApi.getTeamTargets as jest.Mock;

const TARGET: KpiTarget = {
  kpiTargetId: 7,
  orderId: 1,
  orderCode: 'DH001',
  productId: 2,
  productCode: 'MH001',
  productName: 'Áo',
  sewingTeamId: 3,
  sewingTeamName: 'Tổ may 1',
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
  beforeEach(() => getTeamTargets.mockReset());

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
});
