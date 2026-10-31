/** Chuỗi hiển thị màn Doanh thu (UC27, MI-13). Thông báo kết quả thao tác ở constants/messages.ts. */
export const REVENUE_TEXT = {
  pageTitle: 'Doanh thu',
  pageSubtitle:
    'Doanh thu theo tháng/đơn hàng/mã hàng, hệ thống tự tính từ sản lượng hoàn thiện đã nhập kho.',
  filter: {
    periodMonth: 'Tháng',
    orderId: 'Mã đơn hàng',
    productId: 'Mã hàng',
    clear: 'Bỏ lọc',
  },
  export: 'Xuất Excel',
  table: {
    periodMonth: 'Tháng',
    orderCode: 'Đơn hàng',
    product: 'Mã hàng',
    finishedQty: 'SL hoàn thiện',
    revenue: 'Doanh thu',
  },
} as const;
