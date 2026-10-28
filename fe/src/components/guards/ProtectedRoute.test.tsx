import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { Role } from '@/constants/roles';
import { useAuthStore } from '@/store/authStore';
import type { TeamType, UserInfo } from '@/types/auth';
import { ProtectedRoute } from './ProtectedRoute';

// Tránh nạp toàn bộ menu các feature (cần import.meta của Vite); chỉ giữ mục cần cho ca kiểm tra loại tổ.
jest.mock('@/app/menu', () => ({
  MENU_ENTRIES: [
    { key: 'cut', label: 'Bàn cắt', path: '/cutting-lays', roles: ['TeamLeader'], teamTypes: ['Cutting'] },
  ],
}));

const makeUser = (role: Role, teamType: TeamType | null = null): UserInfo => ({
  accountId: 1,
  employeeId: 1,
  username: 'U',
  fullName: 'User',
  role,
  roleName: role,
  teamId: null,
  teamName: null,
  teamType,
  isTemporaryPassword: false,
});

const renderAt = (roles?: Role[], path = '/secret') =>
  render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/login" element={<div>login page</div>} />
        <Route path="/" element={<div>home page</div>} />
        <Route element={<ProtectedRoute roles={roles} />}>
          <Route path="/secret" element={<div>secret page</div>} />
          <Route path="/cutting-lays" element={<div>cutting page</div>} />
        </Route>
      </Routes>
    </MemoryRouter>,
  );

describe('ProtectedRoute', () => {
  it('redirects to login when unauthenticated', () => {
    useAuthStore.setState({ status: 'unauthenticated', user: null });
    renderAt();
    expect(screen.getByText('login page')).toBeInTheDocument();
  });

  it('redirects home when the role is not allowed', () => {
    useAuthStore.setState({ status: 'authenticated', user: makeUser('Worker') });
    renderAt(['Admin']);
    expect(screen.getByText('home page')).toBeInTheDocument();
  });

  it('forces a user with a temporary password to the profile page', () => {
    useAuthStore.setState({ status: 'authenticated', user: { ...makeUser('Admin'), isTemporaryPassword: true } });
    render(
      <MemoryRouter initialEntries={['/secret']}>
        <Routes>
          <Route path="/profile" element={<div>profile page</div>} />
          <Route element={<ProtectedRoute />}>
            <Route path="/secret" element={<div>secret page</div>} />
          </Route>
        </Routes>
      </MemoryRouter>,
    );
    expect(screen.getByText('profile page')).toBeInTheDocument();
  });

  it('renders children when the role is allowed', () => {
    useAuthStore.setState({ status: 'authenticated', user: makeUser('Admin') });
    renderAt(['Admin']);
    expect(screen.getByText('secret page')).toBeInTheDocument();
  });

  it('chặn Tổ trưởng tổ may vào trang của tổ cắt, cho Tổ trưởng tổ cắt vào', () => {
    useAuthStore.setState({ status: 'authenticated', user: makeUser('TeamLeader', 'Sewing') });
    const { unmount } = renderAt(['TeamLeader'], '/cutting-lays');
    expect(screen.getByText('home page')).toBeInTheDocument();
    unmount();

    useAuthStore.setState({ status: 'authenticated', user: makeUser('TeamLeader', 'Cutting') });
    renderAt(['TeamLeader'], '/cutting-lays');
    expect(screen.getByText('cutting page')).toBeInTheDocument();
  });
});
