import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { Role } from '@/constants/roles';
import { useAuthStore } from '@/store/authStore';
import type { UserInfo } from '@/types/auth';
import { ProtectedRoute } from './ProtectedRoute';

const makeUser = (role: Role): UserInfo => ({
  accountId: 1,
  employeeId: 1,
  username: 'U',
  fullName: 'User',
  role,
  roleName: role,
  teamId: null,
  teamName: null,
  teamType: null,
  isTemporaryPassword: false,
});

const renderAt = (roles?: Role[]) =>
  render(
    <MemoryRouter initialEntries={['/secret']}>
      <Routes>
        <Route path="/login" element={<div>login page</div>} />
        <Route path="/" element={<div>home page</div>} />
        <Route element={<ProtectedRoute roles={roles} />}>
          <Route path="/secret" element={<div>secret page</div>} />
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

  it('renders children when the role is allowed', () => {
    useAuthStore.setState({ status: 'authenticated', user: makeUser('Admin') });
    renderAt(['Admin']);
    expect(screen.getByText('secret page')).toBeInTheDocument();
  });
});
