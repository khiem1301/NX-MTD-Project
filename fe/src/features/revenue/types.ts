/**
 * UC27 — Doanh thu, suy ra từ vw_MonthlyRevenue (SL hoàn thiện x đơn giá CMT, hệ thống tự tính).
 * KHÔNG có trường đơn giá CMT ở đây — đó là dữ liệu "mật", chỉ Admin xem được qua màn Hòa vốn
 * (feature break-even), không lộ ra màn doanh thu dù Admin hay Kế toán xem (GBR-02).
 */
export type RevenueSummary = {
  periodMonth: string;
  orderId: number;
  orderCode: string;
  productId: number;
  productCode: string;
  productName: string;
  finishedQty: number;
  revenue: number;
};

export type RevenueFilter = {
  page: number;
  pageSize: number;
  periodMonth?: string;
  orderId?: number;
  productId?: number;
};
