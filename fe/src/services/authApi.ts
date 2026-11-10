import { API } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse } from '@/types/api';
import type { LoginRequest, LoginResult, UserInfo } from '@/types/auth';

export const authApi = {
  login: async (body: LoginRequest): Promise<LoginResult> => {
    const res = await apiClient.post<ApiResponse<LoginResult>>(API.auth.login, body);
    return res.data.data;
  },
  me: async (): Promise<UserInfo> => {
    const res = await apiClient.get<ApiResponse<UserInfo>>(API.auth.me);
    return res.data.data;
  },
  logout: async (): Promise<void> => {
    await apiClient.post(API.auth.logout);
  },
};
