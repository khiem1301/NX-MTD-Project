import type { RolePath } from '@/constants/apiEndpoints';
import { useAuth } from '@/hooks/useAuth';
import { rolePathOf } from '@/utils/rolePath';

/** Tiền tố API theo role đang đăng nhập; trang chỉ mở cho role hợp lệ nên mặc định không bao giờ dùng tới. */
export const useRolePath = (): RolePath => {
  const { user } = useAuth();
  return rolePathOf(user?.role) ?? 'warehouse-keeper';
};

/** Admin có toàn bộ chức năng của Quản đốc: chức năng chỉ khai báo ở api/v1/manager thì Admin gọi chung API đó. */
export const useManagerRolePath = (): RolePath => {
  const role = useRolePath();
  return role === 'admin' ? 'manager' : role;
};
