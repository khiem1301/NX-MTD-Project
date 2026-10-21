export const ROUTES = {
  login: '/login',
  home: '/',
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
