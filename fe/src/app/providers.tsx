import { useEffect, type ReactNode } from 'react';
import { ConfigProvider } from 'antd';
import viVN from 'antd/locale/vi_VN';
import { QueryClientProvider } from '@tanstack/react-query';
import { authApi } from '@/features/auth/api/authApi';
import { queryClient } from '@/services/queryClient';
import { setSessionExpiredHandler } from '@/services/apiClient';
import { tokenStorage } from '@/services/tokenStorage';
import { useAuthStore } from '@/store/authStore';

function AuthBootstrap({ children }: { children: ReactNode }) {
  useEffect(() => {
    const { setSession, clear } = useAuthStore.getState();
    setSessionExpiredHandler(() => {
      clear();
      queryClient.clear();
    });
    if (!tokenStorage.getAccessToken() && !tokenStorage.getRefreshToken()) {
      clear();
      return;
    }
    authApi
      .me()
      .then(setSession)
      .catch(() => {
        tokenStorage.clear();
        clear();
      });
  }, []);
  return <>{children}</>;
}

export function Providers({ children }: { children: ReactNode }) {
  return (
    <ConfigProvider locale={viVN}>
      <QueryClientProvider client={queryClient}>
        <AuthBootstrap>{children}</AuthBootstrap>
      </QueryClientProvider>
    </ConfigProvider>
  );
}
