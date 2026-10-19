// Chỉ ẩn/hiện UI, không phải bảo mật; BE mới là nơi chặn quyền.
import type { ReactNode } from 'react';
import type { Role } from '@/constants/roles';
import { usePermission } from '@/hooks/usePermission';

export function Can({ roles, children }: { roles: Role[]; children: ReactNode }) {
  const { hasRole } = usePermission();
  return hasRole(...roles) ? <>{children}</> : null;
}
