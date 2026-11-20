import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { ERROR_CODES } from '@/constants/errorCodes';
import { MESSAGES } from '@/constants/messages';
import { profileApi, type Profile } from '../api/profileApi';
import { ProfileEditModal } from './ProfileEditModal';

jest.mock('../api/profileApi', () => ({
  profileApi: { changePassword: jest.fn(), get: jest.fn(), update: jest.fn() },
}));
jest.mock('@/services/tokenStorage', () => ({ tokenStorage: { setTokens: jest.fn() } }));

const profile: Profile = {
  employeeId: 1,
  employeeCode: 'ADMIN',
  username: 'ADMIN',
  fullName: 'Quản trị hệ thống',
  gender: null,
  dateOfBirth: null,
  phone: null,
  avatarUrl: null,
  role: 'Admin',
  roleName: 'Giám đốc (Admin)',
  teamCode: null,
  startDate: '2026-01-01',
  currentGradeCode: null,
  isTemporaryPassword: false,
};

const onClose = jest.fn();

const renderModal = () =>
  render(
    <QueryClientProvider client={new QueryClient()}>
      <ProfileEditModal open profile={profile} onClose={onClose} />
    </QueryClientProvider>,
  );

const type = (label: string, value: string) =>
  fireEvent.change(screen.getByLabelText(label), { target: { value } });
const save = () =>
  fireEvent.click(screen.getByText(MESSAGES.common.save, { selector: 'button span' }));
const fillPasswords = (current: string, next: string, again: string) => {
  type(MESSAGES.hr.currentPassword, current);
  type(MESSAGES.hr.newPassword, next);
  type(MESSAGES.hr.confirmPassword, again);
};

describe('ProfileEditModal', () => {
  beforeEach(() => {
    onClose.mockReset();
    (profileApi.update as jest.Mock).mockReset().mockResolvedValue(profile);
    (profileApi.changePassword as jest.Mock)
      .mockReset()
      .mockResolvedValue({ accessToken: 'a', refreshToken: 'r', user: {} });
  });

  it('chỉ sửa số điện thoại, bỏ trống mật khẩu thì không đổi mật khẩu', async () => {
    renderModal();
    type(MESSAGES.hr.phone, '0901234567');
    save();

    await waitFor(() =>
      expect(profileApi.update).toHaveBeenCalledWith({ dateOfBirth: null, phone: '0901234567' }),
    );
    expect(profileApi.changePassword).not.toHaveBeenCalled();
    await waitFor(() => expect(onClose).toHaveBeenCalled());
  });

  it('số điện thoại sai định dạng thì báo lỗi và không gọi API', async () => {
    renderModal();
    type(MESSAGES.hr.phone, '12345');
    save();

    expect(await screen.findByText(/Số điện thoại không hợp lệ/)).toBeInTheDocument();
    expect(profileApi.update).not.toHaveBeenCalled();
  });

  it('chỉ đổi mật khẩu, hồ sơ không đổi thì không gọi API sửa hồ sơ', async () => {
    renderModal();
    fillPasswords('cu', 'moi123', 'moi123');
    save();

    await waitFor(() =>
      expect(profileApi.changePassword).toHaveBeenCalledWith({
        currentPassword: 'cu',
        newPassword: 'moi123',
        confirmPassword: 'moi123',
      }),
    );
    await waitFor(() => expect(onClose).toHaveBeenCalled());
    expect(profileApi.update).not.toHaveBeenCalled();
  });

  it('sửa cả hồ sơ lẫn mật khẩu thì gọi đủ hai API', async () => {
    renderModal();
    type(MESSAGES.hr.phone, '0901234567');
    fillPasswords('cu', 'moi123', 'moi123');
    save();

    await waitFor(() => expect(profileApi.update).toHaveBeenCalledTimes(1));
    expect(profileApi.changePassword).toHaveBeenCalledTimes(1);
  });

  it('sai mật khẩu hiện tại thì báo ở ô đó và chưa lưu hồ sơ', async () => {
    (profileApi.changePassword as jest.Mock).mockRejectedValue({
      success: false,
      errorCode: ERROR_CODES.AuthWrongCurrentPassword,
      message: 'Sai',
    });
    renderModal();
    type(MESSAGES.hr.phone, '0901234567');
    fillPasswords('sai', 'moi123', 'moi123');
    save();

    expect(await screen.findByText(MESSAGES.hr.wrongCurrentPassword)).toBeInTheDocument();
    expect(profileApi.update).not.toHaveBeenCalled();
    expect(onClose).not.toHaveBeenCalled();
  });

  it('nhập lại không khớp thì báo MSG05 và không gọi API nào', async () => {
    renderModal();
    fillPasswords('cu', 'moi123', 'khac');
    save();

    expect(await screen.findByText(MESSAGES.hr.passwordMismatch)).toBeInTheDocument();
    expect(profileApi.changePassword).not.toHaveBeenCalled();
    expect(profileApi.update).not.toHaveBeenCalled();
  });

  it('mới nhập mật khẩu mới mà thiếu các ô còn lại thì báo thiếu', async () => {
    renderModal();
    type(MESSAGES.hr.newPassword, 'moi123');
    save();

    await waitFor(() =>
      expect(screen.getAllByText(MESSAGES.common.required).length).toBeGreaterThanOrEqual(2),
    );
    expect(profileApi.changePassword).not.toHaveBeenCalled();
  });
});
