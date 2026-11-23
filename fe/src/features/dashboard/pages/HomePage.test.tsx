import { render, screen } from '@testing-library/react';
import { useAuth } from '@/hooks/useAuth';
import { HomePage } from './HomePage';

jest.mock('@/hooks/useAuth', () => ({ useAuth: jest.fn() }));
jest.mock('../components/home/AdminHome', () => ({
  AdminHome: () => <div>trang chủ giám đốc</div>,
}));
jest.mock('../components/home/ManagerHome', () => ({
  ManagerHome: () => <div>trang chủ quản đốc</div>,
}));
jest.mock('../components/home/AccountantHome', () => ({
  AccountantHome: () => <div>trang chủ kế toán</div>,
}));
jest.mock('../components/home/WarehouseHome', () => ({
  WarehouseHome: () => <div>trang chủ thủ kho</div>,
}));
jest.mock('../components/home/TeamLeaderHome', () => ({
  TeamLeaderHome: () => <div>trang chủ tổ trưởng</div>,
}));
jest.mock('../components/home/QaqcHome', () => ({ QaqcHome: () => <div>trang chủ qa/qc</div> }));
jest.mock('../components/home/WorkerHome', () => ({
  WorkerHome: () => <div>trang chủ công nhân</div>,
}));
jest.mock('../components/home/ElectricianHome', () => ({
  ElectricianHome: () => <div>trang chủ cơ điện</div>,
}));

const asRole = (role: string) => (useAuth as jest.Mock).mockReturnValue({ user: { role } });

describe('HomePage', () => {
  it.each([
    ['Admin', 'trang chủ giám đốc'],
    ['Manager', 'trang chủ quản đốc'],
    ['Accountant', 'trang chủ kế toán'],
    ['WarehouseKeeper', 'trang chủ thủ kho'],
    ['TeamLeader', 'trang chủ tổ trưởng'],
    ['TeamDeputy', 'trang chủ tổ trưởng'],
    ['QaQc', 'trang chủ qa/qc'],
    ['Worker', 'trang chủ công nhân'],
    ['Electrician', 'trang chủ cơ điện'],
  ])('%s thấy đúng trang chủ của vai trò', (role, text) => {
    asRole(role);
    render(<HomePage />);
    expect(screen.getByText(text)).toBeInTheDocument();
    expect(screen.getAllByText(/^trang chủ /)).toHaveLength(1);
  });

  it('chưa đăng nhập thì không hiện gì', () => {
    (useAuth as jest.Mock).mockReturnValue({ user: null });
    const { container } = render(<HomePage />);
    expect(container).toBeEmptyDOMElement();
  });
});
