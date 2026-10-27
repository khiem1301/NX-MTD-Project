import type { Role } from '@/constants/roles';

export type MenuEntry = {
  key: string;
  label: string;
  path: string;
  roles: readonly Role[] | 'all';
};
