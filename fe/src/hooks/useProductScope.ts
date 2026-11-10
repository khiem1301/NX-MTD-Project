import type { ProductScope } from '@/constants/apiEndpoints';
import { ROLES } from '@/constants/roles';
import { usePermission } from '@/hooks/usePermission';

/** Admin dùng API mã hàng đầy đủ; Kế toán dùng API riêng (danh sách + định mức). */
export const useProductScope = (): ProductScope => {
  const { hasRole } = usePermission();
  return hasRole(ROLES.Accountant) ? 'accountant' : 'admin';
};
