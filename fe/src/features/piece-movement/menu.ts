import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';
import type { MenuEntry } from '@/types/menu';

export const pieceMovementMenu: MenuEntry[] = [
  {
    key: 'piece-movements',
    label: 'Giao sản lượng theo ngày',
    path: ROUTES.pieceMovements,
    roles: [ROLES.TeamLeader, ROLES.TeamDeputy],
    teamTypes: ['Sewing'],
  },
  {
    key: 'my-piece-movements',
    label: 'Sản lượng công đoạn được giao',
    path: ROUTES.myPieceMovements,
    roles: [ROLES.Worker],
    teamTypes: ['Sewing'],
  },
];
