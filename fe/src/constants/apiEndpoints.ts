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
