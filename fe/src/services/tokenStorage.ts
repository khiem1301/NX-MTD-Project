import type { AuthTokens } from '@/types/auth';

const ACCESS_KEY = 'nxmtd.accessToken';
const REFRESH_KEY = 'nxmtd.refreshToken';

export const tokenStorage = {
  getAccessToken: (): string | null => localStorage.getItem(ACCESS_KEY),
  getRefreshToken: (): string | null => localStorage.getItem(REFRESH_KEY),
  setTokens: (tokens: Pick<AuthTokens, 'accessToken' | 'refreshToken'>): void => {
    localStorage.setItem(ACCESS_KEY, tokens.accessToken);
    localStorage.setItem(REFRESH_KEY, tokens.refreshToken);
  },
  clear: (): void => {
    localStorage.removeItem(ACCESS_KEY);
    localStorage.removeItem(REFRESH_KEY);
  },
};
