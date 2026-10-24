import type { Role } from '@/constants/roles';

export type MenuEntry = {
  key: string;
  label: string;
  path: string;
  roles: readonly Role[] | 'all';
  /** Chỉ áp cho Tổ trưởng / Tổ phó: mục này chỉ hiện với tổ thuộc loại này (cắt hay may do loại tổ quyết định). */
  teamTypes?: readonly ('Cutting' | 'Sewing')[];
};
