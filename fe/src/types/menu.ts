import type { Role } from '@/constants/roles';
import type { TeamType } from '@/types/auth';

export type MenuEntry = {
  key: string;
  label: string;
  path: string;
  roles: readonly Role[] | 'all';
  /**
   * Chỉ áp cho vai trò thuộc tổ (Tổ trưởng, Tổ phó, Công nhân): tổ cắt và tổ may cùng vai trò nhưng khác chức năng.
   * Vai trò khác (Quản đốc, Thủ kho...) không bị lọc theo loại tổ.
   */
  teamTypes?: readonly TeamType[];
};
