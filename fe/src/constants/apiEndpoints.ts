/** Tiền tố role trong URL BE: /api/v1/<role>/<resource>. */
export type RolePath = 'admin' | 'manager' | 'warehouse-keeper' | 'team-leader';

const v1 = (path: string): string => `/api/v1/${path}`;

export const API = {
  auth: {
    login: '/api/v1/common/auth/login',
    refresh: '/api/v1/common/auth/refresh',
    logout: '/api/v1/common/auth/logout',
    me: '/api/v1/common/auth/me',
  },
  attendance: {
    syncHealth: '/api/v1/accountant/attendance/sync-health',
    syncSessions: '/api/v1/accountant/attendance/sync-sessions',
    unmappedCodes: '/api/v1/accountant/attendance/unmapped-codes',
    mapCode: '/api/v1/accountant/attendance/map-code',
    timesheets: '/api/v1/accountant/attendance/timesheets',
    timesheetSummary: '/api/v1/accountant/attendance/timesheets/summary',
    adjustments: '/api/v1/accountant/attendance/adjustments',
    importPreview: '/api/v1/accountant/attendance/import/preview',
    import: '/api/v1/accountant/attendance/import',
  },
  teamAttendance: {
    timesheets: '/api/v1/team-leader/attendance/timesheets',
    timesheetSummary: '/api/v1/team-leader/attendance/timesheets/summary',
  },
  myAttendance: {
    timesheets: '/api/v1/common/my-attendance/timesheets',
    timesheetSummary: '/api/v1/common/my-attendance/timesheets/summary',
  },
  lookups: {
    orders: v1('common/lookups/orders'),
    orderLines: (orderId: number) => v1(`common/lookups/orders/${orderId}/lines`),
    teams: v1('common/lookups/teams'),
    materials: v1('common/lookups/materials'),
  },
  warehouse: {
    materials: (role: RolePath) => v1(`${role}/materials`),
    orderMaterials: (role: RolePath) => v1(`${role}/order-materials`),
    materialReceipts: (role: RolePath) => v1(`${role}/material-receipts`),
    materialIssues: (role: RolePath) => v1(`${role}/material-issues`),
    inventories: (role: RolePath) => v1(`${role}/inventories`),
    pieceHoldings: (role: RolePath) => v1(`${role}/piece-holdings`),
  },
  cutting: {
    workOrders: (role: RolePath) => v1(`${role}/work-orders`),
    cuttingLays: (role: RolePath) => v1(`${role}/cutting-lays`),
    cutHandovers: (role: RolePath) => v1(`${role}/cut-handovers`),
  },
} as const;
