import { ROLES } from '@/constants/roles';
import { usePermission } from '@/hooks/usePermission';
import type { OperationRole } from '../types';

/** Admin và Kế toán dùng chung màn công đoạn, mỗi bên gọi API của mình (DEV-ALL-50). */
export const useOperationRole = (): OperationRole => {
  const { hasRole } = usePermission();
  return hasRole(ROLES.Accountant) ? 'accountant' : 'admin';
};
