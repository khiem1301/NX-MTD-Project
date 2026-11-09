import type { ComponentType } from 'react';
import type { RouteObject } from 'react-router-dom';
import type { Role } from '@/constants/roles';
import { ProtectedRoute } from './ProtectedRoute';

/** Một trang của feature: không có roles = mọi người đã đăng nhập. */
export type PageRoute = {
  path: string;
  roles?: Role[];
  page: () => Promise<ComponentType>;
};

/** Mỗi trang bọc <ProtectedRoute roles> riêng và lazy load thành chunk riêng (Rules 4.10). */
export const toPageRoutes = (pages: PageRoute[]): RouteObject[] =>
  pages.map(({ path, roles, page }) => ({
    element: <ProtectedRoute roles={roles} />,
    children: [{ path, lazy: async () => ({ Component: await page() }) }],
  }));
