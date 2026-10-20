import type { RolePath } from '@/constants/apiEndpoints';
import { ROLES, type Role } from '@/constants/roles';

/** Role đăng nhập → tiền tố URL BE. Tổ phó dùng chung API của tổ trưởng. */
export const rolePathOf = (role: Role | undefined): RolePath | null => {
  switch (role) {
    case ROLES.Admin:
      return 'admin';
    case ROLES.Manager:
      return 'manager';
    case ROLES.WarehouseKeeper:
      return 'warehouse-keeper';
    case ROLES.TeamLeader:
    case ROLES.TeamDeputy:
      return 'team-leader';
    default:
      return null;
  }
};
