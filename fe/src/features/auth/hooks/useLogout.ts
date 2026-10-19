// Gọi BE cho đủ luồng nhưng luôn xóa phiên ở FE dù BE lỗi.
import { useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { ROUTES } from '@/constants/routes';
import { tokenStorage } from '@/services/tokenStorage';
import { useAuthStore } from '@/store/authStore';
import { authApi } from '../api/authApi';

export const useLogout = () => {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const clear = useAuthStore((s) => s.clear);
  return async (): Promise<void> => {
    try {
      await authApi.logout();
    } catch {
      /* bỏ qua: đăng xuất FE vẫn phải thành công */
    }
    tokenStorage.clear();
    clear();
    queryClient.clear();
    navigate(ROUTES.login, { replace: true });
  };
};
