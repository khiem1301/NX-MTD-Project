import { toPageRoutes } from '@/components/guards/pageRoutes';
import { ROUTES } from '@/constants/routes';
import { REQUEST_CREATOR_ROLES, REQUEST_HANDLER_ROLES } from './menu';

/** Đơn từ (Khiêm): quyền chi tiết theo (vai trò, loại đơn) do BE kiểm. */
export const requestRoutes = toPageRoutes([
  {
    path: ROUTES.requests,
    roles: [...REQUEST_CREATOR_ROLES],
    page: async () => (await import('./pages/MyRequestsPage')).MyRequestsPage,
  },
  {
    path: ROUTES.requestInbox,
    roles: [...REQUEST_HANDLER_ROLES],
    page: async () => (await import('./pages/RequestInboxPage')).RequestInboxPage,
  },
]);
