import { toPageRoutes } from '@/components/guards/pageRoutes';
import { ROLES } from '@/constants/roles';
import { COST_SUMMARY_ROUTES } from '@/constants/routes';

/** Tổng hợp chi phí (Minh, MI-19, UC12, chỉ Admin, GBR-02). */
export const costSummaryRoutes = toPageRoutes([
  {
    path: COST_SUMMARY_ROUTES.admin,
    roles: [ROLES.Admin],
    page: async () => (await import('./pages/CostSummaryPage')).CostSummaryPage,
  },
]);
