import { useMutation, useQuery } from '@tanstack/react-query';
import { MESSAGES } from '@/constants/messages';
import { useApiMutation } from '@/hooks/useApiMutation';
import { tokenStorage } from '@/services/tokenStorage';
import { useAuthStore } from '@/store/authStore';
import type { ApiError } from '@/types/api';
import type { LoginResult } from '@/types/auth';
import {
  profileApi,
  type ChangePasswordPayload,
  type UpdateProfilePayload,
} from '../api/profileApi';

const PROFILE_KEY = ['profile'];

export const useProfile = () => useQuery({ queryKey: PROFILE_KEY, queryFn: profileApi.get });

/** UC04: tự sửa ngày sinh / số điện thoại, xong thì tải lại hồ sơ. */
export const useUpdateProfile = () =>
  useApiMutation((body: UpdateProfilePayload) => profileApi.update(body), {
    invalidate: [PROFILE_KEY],
    successMessage: MESSAGES.common.saved,
  });

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
