import { toPageRoutes } from '@/components/guards/pageRoutes';
import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';

/** Giao / thu hồi BTP với công nhân (Thái, TH-19). */
export const pieceMovementRoutes = toPageRoutes([
  {
    path: ROUTES.pieceMovements,
    roles: [ROLES.TeamLeader, ROLES.TeamDeputy],
    page: async () => (await import('./pages/LeaderPieceMovementsPage')).LeaderPieceMovementsPage,
  },
  {
    path: ROUTES.myPieceMovements,
    roles: [ROLES.Worker],
    page: async () => (await import('./pages/MyPieceMovementsPage')).MyPieceMovementsPage,
  },
]);
