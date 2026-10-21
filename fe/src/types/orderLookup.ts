/** Tra cứu đơn / dòng đơn / mã hàng cho luồng khác. Cố ý KHÔNG có đơn giá CMT (D26). */
export type OrderLookup = {
  orderId: number;
  orderCode: string;
  customerName: string;
  dueDate: string;
  status: string;
  materialStatus: string;
};

export type OrderLineLookup = {
  orderLineId: number;
  orderId: number;
  productId: number;
  productCode: string;
  productName: string;
  color: string;
  sizeCode: string;
  orderedQty: number;
};

export type ProductLookup = { productId: number; productCode: string; productName: string };
