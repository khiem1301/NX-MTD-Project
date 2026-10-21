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
  profile: { base: v1('common/profile'), changePassword: v1('common/profile/change-password') },
  employees: {
    base: (role: RolePath) => v1(`${role}/employees`),
    byId: (role: RolePath, id: number) => v1(`${role}/employees/${id}`),
    resign: (id: number) => v1(`admin/employees/${id}/resign`),
    lock: (id: number) => v1(`admin/employees/${id}/account/lock`),
    unlock: (id: number) => v1(`admin/employees/${id}/account/unlock`),
    resetPassword: (id: number) => v1(`admin/employees/${id}/account/reset-password`),
  },
  teams: {
    base: (role: RolePath) => v1(`${role}/teams`),
    byId: (role: RolePath, id: number) => v1(`${role}/teams/${id}`),
    action: (role: RolePath, id: number, action: 'discontinue' | 'reactivate' | 'transfer-in' | 'appoint') =>
      v1(`${role}/teams/${id}/${action}`),
  },
  hrLookups: { teams: v1('common/hr-lookups/teams'), teamMembers: v1('common/hr-lookups/team-members') },
  customers: { base: v1('admin/customers'), byId: (id: number) => v1(`admin/customers/${id}`) },
  products: {
    base: v1('admin/products'),
    sampleImage: v1('admin/products/sample-image'),
    byId: (id: number) => v1(`admin/products/${id}`),
    discontinue: (id: number) => v1(`admin/products/${id}/discontinue`),
    reactivate: (id: number) => v1(`admin/products/${id}/reactivate`),
  },
  customerOrders: {
    base: v1('admin/customer-orders'),
    nextCode: v1('admin/customer-orders/next-code'),
    byId: (id: number) => v1(`admin/customer-orders/${id}`),
    cancel: (id: number) => v1(`admin/customer-orders/${id}/cancel`),
    stop: (id: number) => v1(`admin/customer-orders/${id}/stop`),
  },
  /** Tra cứu cho luồng khác: không có đơn giá (D26). */
  orderLookups: {
    orders: v1('common/order-lookups/orders'),
    orderLines: (orderId: number) => v1(`common/order-lookups/orders/${orderId}/lines`),
    products: v1('common/order-lookups/products'),
  },
  requests: {
    base: v1('common/requests'),
    types: v1('common/requests/types'),
    byId: (id: number) => v1(`common/requests/${id}`),
    cancel: (id: number) => v1(`common/requests/${id}/cancel`),
    inbox: v1('common/request-inbox'),
    approve: (id: number) => v1(`common/request-inbox/${id}/approve`),
    reject: (id: number) => v1(`common/request-inbox/${id}/reject`),
    complete: (id: number) => v1(`common/request-inbox/${id}/complete`),
  },
  attachments: {
    base: v1('common/attachments'),
    content: (id: number) => v1(`common/attachments/${id}/content`),
    byId: (id: number) => v1(`common/attachments/${id}`),
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
    purchaseOrders: (role: RolePath) => v1(`${role}/purchase-orders`),
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
