import { ROLES } from '@/constants/roles';
import type { MenuEntry } from '@/types/menu';
import { PRODUCTION_TEXT } from './constants';
import { PRODUCTION_ROUTES } from './routes';

const teamRoles = [ROLES.TeamLeader, ROLES.TeamDeputy];

export const productionMenu: MenuEntry[] = [
  { key: 'operations', label: PRODUCTION_TEXT.operations.title, path: PRODUCTION_ROUTES.operations, roles: [ROLES.Accountant] },
  {
    key: 'admin-piece-rates',
    label: PRODUCTION_TEXT.pieceRates.adminTitle,
    path: PRODUCTION_ROUTES.adminPieceRates,
    roles: [ROLES.Admin],
  },
  { key: 'operation-assignments', label: PRODUCTION_TEXT.assignments.title, path: PRODUCTION_ROUTES.assignments, roles: teamRoles, teamTypes: ['Sewing'] },
  { key: 'output-records', label: PRODUCTION_TEXT.output.title, path: PRODUCTION_ROUTES.output, roles: teamRoles, teamTypes: ['Sewing'] },
  { key: 'defect-records', label: PRODUCTION_TEXT.defects.title, path: PRODUCTION_ROUTES.defects, roles: teamRoles, teamTypes: ['Sewing'] },
  { key: 'qc-batches', label: PRODUCTION_TEXT.qc.title, path: PRODUCTION_ROUTES.qcBatches, roles: [ROLES.QaQc] },
  {
    key: 'manager-output-records',
    label: PRODUCTION_TEXT.output.managerTitle,
    path: PRODUCTION_ROUTES.managerOutput,
    roles: [ROLES.Manager],
  },
  {
    key: 'manager-defect-records',
    label: PRODUCTION_TEXT.defects.managerTitle,
    path: PRODUCTION_ROUTES.managerDefects,
    roles: [ROLES.Manager],
  },
  {
    key: 'manager-qc-batches',
    label: PRODUCTION_TEXT.qc.managerTitle,
    path: PRODUCTION_ROUTES.managerQcBatches,
    roles: [ROLES.Manager],
  },
  { key: 'my-defects', label: PRODUCTION_TEXT.myDefects.title, path: PRODUCTION_ROUTES.myDefects, roles: [ROLES.Worker] },
];
