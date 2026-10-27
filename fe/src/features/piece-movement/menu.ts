import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';
import type { MenuEntry } from '@/types/menu';

export const pieceMovementMenu: MenuEntry[] = [
  {
    key: 'piece-movements',
    label: 'Giao việc cho công nhân',
    path: ROUTES.pieceMovements,
    roles: [ROLES.TeamLeader, ROLES.TeamDeputy],
    teamTypes: ['Sewing'],
  },
  {
    key: 'my-piece-movements',
    label: 'Việc được giao cho tôi',
    path: ROUTES.myPieceMovements,
    roles: [ROLES.Worker],
    teamTypes: ['Sewing'],
  },
];
