export type OrderProgress = {
  orderId: number;
  orderCode: string;
  customerName: string;
  dueDate: string;
  daysLeft: number;
  status: string;
  materialStatus: string;
  isDelayRisk: boolean;
  delayRiskSince: string | null;
  orderedQty: number;
  cutQty: number;
  cutHandedToWarehouseQty: number;
  issuedToSewingQty: number;
  finishedQty: number;
  finishedReceivedQty: number;
  deliveredQty: number;
  reworkQty: number;
  scrapQty: number;
  defectRatePct: number;
};

export type LineProgress = {
  orderLineId: number;
  productCode: string;
  color: string;
  sizeCode: string;
  orderedQty: number;
  cutQty: number;
  cutHandedToWarehouseQty: number;
  issuedToSewingQty: number;
  finishedQty: number;
  finishedReceivedQty: number;
  deliveredQty: number;
  reworkQty: number;
  scrapQty: number;
  defectRatePct: number | null;
};

// ---------- Trang chủ theo vai trò (DEV-ALL-170), khớp HomeDtos.cs ở BE ----------

/** Tiền tháng này và tháng trước; null = tháng đó chưa có số liệu (vd chưa nhập chi phí). */
export type HomeMoney = { thisMonth: number | null; lastMonth: number | null };

export type AdminHome = {
  pieceRatesPending: number;
  payrollsPending: number;
  ordersRunning: number;
  revenue: HomeMoney;
  cost: HomeMoney;
  orders: OrderProgress[];
};

/** Một đơn từ trong hộp đơn từ của Quản đốc (chỉ các trường trang chủ dùng). */
export type HomeRequestItem = {
  requestId: number;
  requestCode: string;
  requestType: string;
  title: string | null;
  createdByName: string | null;
  teamCode: string | null;
  createdAt: string;
};

export type ManagerHome = {
  receiptsPending: number;
  requestsPending: number;
  /** Mã tổ có tổ trưởng chưa có giờ vào sau giờ kiểm 08:00. */
  leaderAbsentTeams: string[];
  orders: OrderProgress[];
  requests: HomeRequestItem[];
};

/** Một dòng trong danh sách trang chủ; tagColor: red | orange | blue | green | gray. */
export type HomeItem = { title: string; meta: string | null; tag: string; tagColor: string };

export type AccountantHome = {
  missingPunch: number;
  pieceRatesMissing: number;
  pieceRatesRejected: number;
  purchasesUnpaid: number;
  ordersNotSet: number;
  costEntered: boolean;
  payrollStatus: string | null;
  orders: HomeItem[];
  purchases: HomeItem[];
};

export type WarehouseHome = {
  incoming: number;
  workOrdersNoIssue: number;
  requestsToIssue: number;
  cutHandovers: number;
  fgHandovers: number;
  returns: number;
  incomingItems: HomeItem[];
  pendingItems: HomeItem[];
};

export type HomeWorkOrder = {
  workOrderId: number;
  code: string;
  orderCode: string;
  status: string;
  plannedQty: number;
  cutQty: number;
};

export type CuttingHome = {
  issuesPending: number;
  cutNotHanded: number;
  leftoverNotReturned: number;
  workOrders: HomeWorkOrder[];
};

export type SewingHome = {
  kpiTarget: number;
  kpiActual: number;
  btpPending: number;
  unassignedOperations: number;
  allocationsPending: number;
  mealSuggested: number;
  mealConfirmed: number | null;
  mealLocked: boolean;
  btpIssues: HomeItem[];
};

export type TeamLeaderHome = {
  teamType: string | null;
  cutting: CuttingHome | null;
  sewing: SewingHome | null;
};

export type QaqcHome = {
  onLineQty: number;
  notReceivedQty: number;
  handoversPending: number;
  lines: HomeItem[];
  handovers: HomeItem[];
};

export type WorkerHome = {
  /** null = công nhân không khoán sản phẩm (không có lương tạm tính). */
  estimatedToday: number | null;
  estimatedMonth: number | null;
  latestPayslip: { periodMonth: string; netPay: number } | null;
  allocationsPending: number;
  allocations: HomeItem[];
  defects: HomeItem[];
  mealHasAttendance: boolean;
  mealWillEat: boolean;
  mealLocked: boolean;
};

export type HomeTicket = {
  requestId: number;
  code: string;
  requestType: string;
  status: string;
  title: string | null;
  createdAt: string;
};

export type ElectricianHome = { tickets: HomeTicket[] };
