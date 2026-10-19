import { useMutation } from '@tanstack/react-query';
import { tokenStorage } from '@/services/tokenStorage';
import { useAuthStore } from '@/store/authStore';
import type { ApiError } from '@/types/api';
import type { LoginRequest, LoginResult } from '@/types/auth';
import { authApi } from '../api/authApi';

export const useLogin = () => {
  const setSession = useAuthStore((s) => s.setSession);
  return useMutation<LoginResult, ApiError, LoginRequest>({
    mutationFn: authApi.login,
    onSuccess: (data) => {
      tokenStorage.setTokens(data);
      setSession(data.user);
    },
  });
};
