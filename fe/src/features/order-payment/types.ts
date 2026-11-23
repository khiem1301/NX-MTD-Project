import type { AuditEntry } from '@/types/attachment';

/** Trạng thái thanh toán đơn hàng khách, BE tính từ giá trị chốt và các lần thanh toán. */
export type OrderPaymentState = 'NotSet' | 'Unpaid' | 'Partial' | 'Settled';
export type OrderPaymentMethod = 'BankTransfer' | 'Cash';

export type OrderPaymentListItem = {
  orderId: number;
  orderCode: string;
  customerName: string;
  orderStatus: string;
  /** Tổng tiền gia công cả đơn (Σ số lượng × đơn giá); chỉ Kế toán thấy, Quản đốc không. */
  orderAmount: number;
  /** Giá trị chốt cả đơn do Kế toán nhập; âm = mình phải đền hợp đồng; null = chưa chốt. */
  finalAmount: number | null;
  /** Tổng đã thanh toán: dương = khách đã trả, âm = mình đã trả khách. */
  paidAmount: number;
  /** Giá trị chốt − đã thanh toán: dương = khách còn phải trả; âm = mình còn phải trả khách. */
  remaining: number | null;
  state: OrderPaymentState;
};

/** Một lần thanh toán; canEdit: trong 24 giờ kể từ lúc nhập. Không xóa được. */
export type OrderPaymentLine = {
  orderPaymentId: number;
  paymentDate: string;
  amount: number;
  paymentMethod: OrderPaymentMethod;
  paymentNote: string | null;
  createdAt: string;
  updatedAt: string | null;
  editReason: string | null;
  canEdit: boolean;
};

export type OrderPaymentDetail = OrderPaymentListItem & {
  finalAmountReason: string | null;
  rowVer: string;
  payments: OrderPaymentLine[];
  history: AuditEntry[];
};

export type OrderPaymentQuery = {
  page: number;
  pageSize: number;
  keyword?: string;
  state?: OrderPaymentState;
};

export type SetFinalAmountPayload = { amount: number; reason: string; rowVer: string };

export type AddOrderPaymentPayload = {
  paymentDate: string;
  amount: number;
  paymentMethod: OrderPaymentMethod;
  paymentNote: string | null;
  rowVer: string;
};

export type UpdateOrderPaymentPayload = AddOrderPaymentPayload & { reason: string };
