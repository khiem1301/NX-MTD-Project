import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { PASSWORD_MAX_LENGTH } from '@/constants/limits';
import { MESSAGES } from '@/constants/messages';
import { profileApi } from '../api/profileApi';
import { ChangePasswordForm } from './ChangePasswordForm';

jest.mock('../api/profileApi', () => ({
  profileApi: { changePassword: jest.fn(), get: jest.fn() },
}));
jest.mock('@/services/tokenStorage', () => ({ tokenStorage: { setTokens: jest.fn() } }));

const renderForm = () =>
  render(
    <QueryClientProvider client={new QueryClient()}>
      <ChangePasswordForm />
    </QueryClientProvider>,
  );

const fill = (current: string, next: string, again: string) => {
  fireEvent.change(screen.getByLabelText('Mật khẩu hiện tại'), { target: { value: current } });
  fireEvent.change(screen.getByLabelText('Mật khẩu mới'), { target: { value: next } });
  fireEvent.change(screen.getByLabelText('Nhập lại mật khẩu mới'), { target: { value: again } });
  fireEvent.click(screen.getByText('Đổi mật khẩu', { selector: 'button span' }));
};

describe('ChangePasswordForm', () => {
  beforeEach(() => (profileApi.changePassword as jest.Mock).mockReset());

  it('nhập lại không khớp thì báo MSG05 và không gọi API', async () => {
    renderForm();
    fill('cu', 'moi123', 'khac');

    expect(await screen.findByText('Mật khẩu nhập lại không khớp.')).toBeInTheDocument();
    expect(profileApi.changePassword).not.toHaveBeenCalled();
  });

  it('khớp thì gọi API đổi mật khẩu', async () => {
    (profileApi.changePassword as jest.Mock).mockResolvedValue({
      accessToken: 'a',
      refreshToken: 'r',
      user: {},
    });
    renderForm();
    fill('cu', 'moi123', 'moi123');

    await waitFor(() =>
      expect(profileApi.changePassword).toHaveBeenCalledWith({
        currentPassword: 'cu',
        newPassword: 'moi123',
        confirmPassword: 'moi123',
      }),
    );
  });

  it('mật khẩu mới đúng 50 ký tự thì gọi API', async () => {
    (profileApi.changePassword as jest.Mock).mockResolvedValue({
      accessToken: 'a',
      refreshToken: 'r',
      user: {},
    });
    const password = 'a'.repeat(PASSWORD_MAX_LENGTH);
    renderForm();
    fill('cu', password, password);

    await waitFor(() => expect(profileApi.changePassword).toHaveBeenCalledTimes(1));
  });

  it('mật khẩu mới 51 ký tự thì báo quá dài và không gọi API', async () => {
    const password = 'a'.repeat(PASSWORD_MAX_LENGTH + 1);
    renderForm();
    fill('cu', password, password);

    expect(await screen.findByText(MESSAGES.hr.passwordTooLong)).toBeInTheDocument();
    expect(profileApi.changePassword).not.toHaveBeenCalled();
  });
});
