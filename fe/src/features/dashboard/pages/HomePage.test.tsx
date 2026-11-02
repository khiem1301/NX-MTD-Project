import { render, screen } from '@testing-library/react';
import { useAuth } from '@/hooks/useAuth';
import { HomePage } from './HomePage';

jest.mock('@/hooks/useAuth', () => ({ useAuth: jest.fn() }));
jest.mock('../components/OrderProgressPanel', () => ({ OrderProgressPanel: () => <div>tiến độ</div> }));
jest.mock('@/features/revenue', () => ({ RevenuePanel: () => <div>doanh thu</div> }));
jest.mock('@/features/kpi', () => ({ TeamKpiPanel: () => <div>kpi của tổ</div> }));

const asUser = (role: string, teamType: string | null) =>
  (useAuth as jest.Mock).mockReturnValue({ user: { role, teamType } });

describe('HomePage', () => {
  it.each([
    ['TeamLeader', 'Sewing'],
    ['TeamDeputy', 'Sewing'],
  ])('%s tổ may xem được KPI của tổ', async (role, teamType) => {
    asUser(role, teamType);
    render(<HomePage />);
    expect(await screen.findByText('kpi của tổ')).toBeInTheDocument();
  });

  it.each([
    ['TeamLeader', 'Cutting'],
    ['Worker', 'Sewing'],
    ['Manager', 'Office'],
  ])('%s %s không có khối KPI của tổ', async (role, teamType) => {
    asUser(role, teamType);
    render(<HomePage />);
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(screen.queryByText('kpi của tổ')).not.toBeInTheDocument();
  });
});
