import { API } from '@/constants/apiEndpoints';
import type { Role } from '@/constants/roles';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse } from '@/types/api';
import type { LoginResult } from '@/types/auth';

export type Profile = {
  employeeId: number;
  employeeCode: string;
  fullName: string;
  gender: string | null;
  dateOfBirth: string | null;
  phone: string | null;
  avatarUrl: string | null;
  role: Role;
  roleName: string;
  teamName: string | null;
  startDate: string;
  currentGradeCode: string | null;
  isTemporaryPassword: boolean;
};

export type ChangePasswordPayload = {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
};

export const profileApi = {
  get: async (): Promise<Profile> =>
    (await apiClient.get<ApiResponse<Profile>>(API.profile.base)).data.data,
  /** BE trả cặp token mới cho phiên hiện tại (D36); các phiên khác bị đẩy ra. */
  changePassword: async (body: ChangePasswordPayload): Promise<LoginResult> =>
    (await apiClient.post<ApiResponse<LoginResult>>(API.profile.changePassword, body)).data.data,
};
