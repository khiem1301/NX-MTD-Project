import axios, { type AxiosError, type AxiosRequestConfig } from 'axios';
import { API } from '@/constants/apiEndpoints';
import { ERROR_CODES } from '@/constants/errorCodes';
import { MESSAGES } from '@/constants/messages';
import { env } from '@/services/env';
import { tokenStorage } from '@/services/tokenStorage';
import type { ApiError, ApiResponse } from '@/types/api';
import type { AuthTokens } from '@/types/auth';

type RetriableConfig = AxiosRequestConfig & { _retried?: boolean };

export const apiClient = axios.create({ baseURL: env.apiBaseUrl });

let refreshing: Promise<string | null> | null = null;
let onSessionExpired: (() => void) | null = null;

/** AuthBootstrap đăng ký hàm này để xóa state đăng nhập khi refresh thất bại. */
export const setSessionExpiredHandler = (handler: () => void): void => {
  onSessionExpired = handler;
};

const isAuthUrl = (url?: string): boolean =>
  !!url && (url.includes(API.auth.login) || url.includes(API.auth.refresh));

const refreshAccessToken = async (): Promise<string | null> => {
  const refreshToken = tokenStorage.getRefreshToken();
  if (!refreshToken) return null;
  try {
    // Dùng axios trần (không interceptor) để tránh vòng lặp.
    const res = await axios.post<ApiResponse<AuthTokens>>(`${env.apiBaseUrl}${API.auth.refresh}`, {
      refreshToken,
    });
    tokenStorage.setTokens(res.data.data);
    return res.data.data.accessToken;
  } catch {
    return null;
  }
};

const normalizeError = (error: AxiosError<ApiError>): ApiError => {
  const body = error.response?.data;
  if (body && body.success === false) return body;
  if (!error.response) {
    return {
      success: false,
      errorCode: ERROR_CODES.CommonNetworkError,
      message: MESSAGES.common.networkError,
    };
  }
  return {
    success: false,
    errorCode: ERROR_CODES.CommonInternalError,
    message: MESSAGES.common.unknownError,
  };
};

apiClient.interceptors.request.use((config) => {
  const token = tokenStorage.getAccessToken();
  if (token) config.headers.set('Authorization', `Bearer ${token}`);
  return config;
});

apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<ApiError>) => {
    const original = error.config as RetriableConfig | undefined;
    if (
      error.response?.status === 401 &&
      original &&
      !original._retried &&
      !isAuthUrl(original.url)
    ) {
      original._retried = true;
      refreshing ??= refreshAccessToken().finally(() => {
        refreshing = null;
      });
      const token = await refreshing;
      if (token) {
        original.headers = { ...original.headers, Authorization: `Bearer ${token}` };
        return apiClient(original);
      }
      tokenStorage.clear();
      onSessionExpired?.();
    }
    return Promise.reject(normalizeError(error));
  },
);
