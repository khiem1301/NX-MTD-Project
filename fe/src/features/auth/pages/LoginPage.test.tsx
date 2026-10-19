import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { useAuthStore } from '@/store/authStore';
import { LoginPage } from './LoginPage';

jest.mock('@/services/env', () => ({ env: { apiBaseUrl: 'http://localhost' } }));

describe('LoginPage', () => {
  it('shows required-field messages when submitted empty', async () => {
    useAuthStore.setState({ status: 'unauthenticated', user: null });
    render(
      <QueryClientProvider client={new QueryClient()}>
        <MemoryRouter>
          <LoginPage />
        </MemoryRouter>
      </QueryClientProvider>,
    );

    fireEvent.click(screen.getByRole('button', { name: /Đăng nhập/ }));

    expect(await screen.findByText('Vui lòng nhập tên đăng nhập.')).toBeInTheDocument();
    expect(await screen.findByText('Vui lòng nhập mật khẩu.')).toBeInTheDocument();
  });
});
