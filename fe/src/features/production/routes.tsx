import { toPageRoutes } from '@/components/guards/pageRoutes';
import { ROLES } from '@/constants/roles';
import { PRODUCTION_ROUTES } from '@/constants/routes';

/** Công đoạn, đơn giá, phân công, sản lượng, QC, hàng hỏng (Minh làm hộ Kiều, KI-01..KI-11). */
export const productionRoutes = toPageRoutes([
  {
    path: PRODUCTION_ROUTES.operations,
    roles: [ROLES.Accountant, ROLES.Admin],
    page: async () => (await import('./pages/OperationsPage')).OperationsPage,
  },
  {
    path: PRODUCTION_ROUTES.adminPieceRates,
    roles: [ROLES.Admin],
    page: async () => (await import('./pages/AdminPieceRatesPage')).AdminPieceRatesPage,
  },
  {
    path: PRODUCTION_ROUTES.orderLineTeams,
    roles: [ROLES.Admin, ROLES.Manager],
    page: async () => (await import('./pages/OrderLineTeamsPage')).OrderLineTeamsPage,
  },
  {
    path: PRODUCTION_ROUTES.assignments,
    roles: [ROLES.TeamLeader, ROLES.TeamDeputy],
    page: async () => (await import('./pages/OperationAssignmentsPage')).OperationAssignmentsPage,
  },
  {
    path: PRODUCTION_ROUTES.output,
    roles: [ROLES.TeamLeader, ROLES.TeamDeputy],
    page: async () => (await import('./pages/OutputEntryPage')).OutputEntryPage,
  },
  {
    path: PRODUCTION_ROUTES.defects,
    roles: [ROLES.TeamLeader, ROLES.TeamDeputy],
    page: async () => (await import('./pages/DefectRecordsPage')).DefectRecordsPage,
  },
  {
    path: PRODUCTION_ROUTES.qcBatches,
    roles: [ROLES.QaQc],
    page: async () => (await import('./pages/QcBatchesPage')).QcBatchesPage,
  },
  {
    path: PRODUCTION_ROUTES.managerOutput,
    roles: [ROLES.Admin, ROLES.Manager],
    page: async () => (await import('./pages/ManagerOutputPage')).ManagerOutputPage,
  },
  {
    path: PRODUCTION_ROUTES.managerDefects,
    roles: [ROLES.Admin, ROLES.Manager],
    page: async () => (await import('./pages/ManagerDefectsPage')).ManagerDefectsPage,
  },
  {
    path: PRODUCTION_ROUTES.managerQcBatches,
    roles: [ROLES.Admin, ROLES.Manager],
    page: async () => (await import('./pages/ManagerQcBatchesPage')).ManagerQcBatchesPage,
  },
  {
    path: PRODUCTION_ROUTES.myDefects,
    roles: [ROLES.Worker],
    page: async () => (await import('./pages/MyDefectsPage')).MyDefectsPage,
  },
]);
