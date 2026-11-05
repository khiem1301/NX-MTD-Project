import type { PageParams } from '@/types/api';

export type DispatchNoteListItem = {
  dispatchNoteId: number;
  dispatchCode: string;
  orderId: number;
  orderCode: string;
  customerName: string;
  deliveryDate: string;
  status: string;
  createdAt: string;
  totalIssueQty: number;
};

export type DispatchNoteLine = {
  dispatchNoteLineId: number;
  orderLineId: number;
  productCode: string;
  color: string;
  sizeCode: string;
  issueQty: number;
};

export type DispatchNote = Omit<DispatchNoteListItem, 'totalIssueQty'> & {
  deliveryAddress: string;
  note: string | null;
  rejectReason: string | null;
  dispatchedByName: string | null;
  dispatchedAt: string | null;
  customerReceivedDate: string | null;
  lines: DispatchNoteLine[];
  /** Phiên bản phiếu đang xem; gửi lại khi hủy, xuất, từ chối, xác nhận giao để phát hiện người khác đã đổi phiếu. */
  rowVer: string;
};

export type DispatchableLine = {
  orderLineId: number;
  productCode: string;
  color: string;
  sizeCode: string;
  orderedQty: number;
  issuedQty: number;
  remainingQty: number;
  availableQty: number;
};

export type DispatchNoteQuery = PageParams & { status?: string; orderId?: number };

export type CreateDispatchPayload = {
  orderId: number;
  deliveryDate: string;
  deliveryAddress: string;
  note?: string;
  lines: { orderLineId: number; issueQty: number }[];
};

export type ConfirmDeliveryPayload = {
  customerReceivedDate: string;
  rowVer: string;
};
