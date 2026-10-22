export const ROUTES = {
  login: '/login',
  home: '/',
  // Nhân sự (Khiêm)
  profile: '/profile',
  adminEmployees: '/admin/employees',
  managerEmployees: '/manager/employees',
  employeeDetail: (base: string, id: number | string) => `${base}/${id}`,
  teams: '/teams',
  // Mã hàng, đơn hàng (Khiêm)
  products: '/admin/products',
  customers: '/admin/customers',
  orders: '/admin/orders',
  orderNew: '/admin/orders/new',
  orderDetail: (id: number | string) => `/admin/orders/${id}`,
  orderEdit: (id: number | string) => `/admin/orders/${id}/edit`,
  // Đơn từ (Khiêm)
  requests: '/requests',
  requestInbox: '/requests/inbox',
  // Kho, cắt (Thái)
  materials: '/warehouse/materials',
  orderMaterials: '/warehouse/order-materials',
  materialReceipts: '/warehouse/material-receipts',
  materialIssues: '/warehouse/material-issues',
  inventories: '/warehouse/inventories',
  pieceHoldings: '/warehouse/piece-holdings',
  workOrders: '/cutting/work-orders',
  cuttingLays: '/cutting/lays',
  cutHandovers: '/cutting/handovers',
} as const;
