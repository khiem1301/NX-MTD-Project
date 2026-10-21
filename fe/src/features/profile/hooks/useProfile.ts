import { useMutation, useQuery } from '@tanstack/react-query';
import { tokenStorage } from '@/services/tokenStorage';
import { useAuthStore } from '@/store/authStore';
import type { ApiError } from '@/types/api';
import type { LoginResult } from '@/types/auth';
import { profileApi, type ChangePasswordPayload } from '../api/profileApi';

export const useProfile = () => useQuery({ queryKey: ['profile'], queryFn: profileApi.get });

/** Đổi mật khẩu xong lưu cặp token mới + thông tin người dùng (hết cờ mật khẩu tạm), cùng cách useLogin. */
export const useChangePassword = () => {
  const setSession = useAuthStore((s) => s.setSession);
  return useMutation<LoginResult, ApiError, ChangePasswordPayload>({
    mutationFn: (body) => profileApi.changePassword(body),
    onSuccess: (data) => {
      tokenStorage.setTokens(data);
      setSession(data.user);
    },
  });
};
