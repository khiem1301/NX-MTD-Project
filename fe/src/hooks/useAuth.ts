import { useAuthStore } from '@/store/authStore';

export const useAuth = () => {
  const status = useAuthStore((s) => s.status);
  const user = useAuthStore((s) => s.user);
  return { status, user, isAuthenticated: status === 'authenticated' };
};
