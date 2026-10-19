import type { Role } from '@/constants/roles';
import { useAuth } from '@/hooks/useAuth';

export const usePermission = () => {
  const { user } = useAuth();
  return { hasRole: (...roles: Role[]): boolean => !!user && roles.includes(user.role) };
};
