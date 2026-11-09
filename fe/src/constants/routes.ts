export const ROUTES = {
  login: '/login',
  home: '/',
  // Nhân sự (Khiêm)
  profile: '/profile',
  adminEmployees: '/admin/employees',
  managerEmployees: '/manager/employees',
  employeeDetail: (base: string, id: number | string) => `${base}?id=${id}`,
  teams: '/teams',
  // Mã hàng, đơn hàng (Khiêm)
  products: '/admin/products',
  accountantProducts: '/accounting/products',
  customers: '/admin/customers',
  orders: '/admin/orders',
  orderNew: '/admin/orders/new',
  orderDetail: (id: number | string) => `/admin/orders/${id}`,
  orderEdit: (id: number | string) => `/admin/orders/${id}/edit`,
  managerOrders: '/manager/orders',
  managerOrderDetail: (id: number | string) => `/manager/orders/${id}`,
  // Đơn từ (Khiêm)
  requests: '/requests',
  requestInbox: '/requests/inbox',
  // Kho, cắt (Thái)
  materials: '/warehouse/materials',
  orderMaterials: '/warehouse/order-materials',
  incomingMaterials: '/warehouse/incoming-materials',
  materialReceipts: '/warehouse/material-receipts',
  shortDeliveries: '/warehouse/short-deliveries',
  materialIssues: '/warehouse/material-issues',
  fgHandovers: '/warehouse/fg-handovers',
  dispatchNotes: '/warehouse/dispatch-notes',
  stocktakes: '/warehouse/stocktakes',
  vehicleLog: '/accounting/vehicle-log',
  notifications: '/notifications',
  grades: '/hr/grades',
  auditLogs: '/admin/audit-logs',
  materialReturns: '/warehouse/material-returns',
  pieceMovements: '/warehouse/piece-movements',
  myPieceMovements: '/my-piece-movements',
  inventories: '/warehouse/inventories',
  pieceHoldings: '/warehouse/piece-holdings',
  workOrders: '/cutting/work-orders',
  cuttingLays: '/cutting/lays',
  cutHandovers: '/cutting/handovers',
} as const;

export const ATTENDANCE_ROUTES = {
  attendance: '/attendance',
  teamAttendance: '/team/attendance',
  myAttendance: '/my/attendance',
} as const;

export const MEAL_ROUTES = {
  teamMeal: '/team/meals',
  summary: '/meals/summary',
  settings: '/admin/meal-settings',
} as const;

export const PAYROLL_ROUTES = {
  accountant: '/payrolls',
  approval: '/payrolls/approval',
  myPayslip: '/my/payslips',
  myEstimatedPay: '/my/estimated-pay',
} as const;

export const PURCHASE_ROUTES = {
  suppliers: '/suppliers',
  orders: '/purchase-orders',
  payments: '/purchase-payments',
} as const;

export const MONTHLY_COST_ROUTES = {
  accountant: '/monthly-costs',
} as const;

export const KPI_ROUTES = {
  targets: '/kpi/targets',
} as const;

export const PRODUCTION_ROUTES = {
  operations: '/operations',
  adminPieceRates: '/admin/piece-rates',
  assignments: '/operation-assignments',
  orderLineTeams: '/order-line-teams',
  output: '/output-records',
  managerOutput: '/manager/output-records',
  defects: '/defect-records',
  managerDefects: '/manager/defect-records',
  qcBatches: '/qc-batches',
  managerQcBatches: '/manager/qc-batches',
  myDefects: '/my-defects',
} as const;

export const REVENUE_ROUTES = {
  accountant: '/revenue',
  admin: '/admin/revenue',
} as const;

export const BREAK_EVEN_ROUTES = {
  admin: '/admin/break-even',
} as const;

export const COST_SUMMARY_ROUTES = {
  admin: '/admin/cost-summary',
} as const;

export const ACCOUNTING_REPORT_ROUTES = {
  accountant: '/accountant/accounting-reports',
} as const;
