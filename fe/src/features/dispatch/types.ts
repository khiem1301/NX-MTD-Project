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
  totalReceivedQty: number | null;
};

export type DispatchNoteLine = {
  dispatchNoteLineId: number;
  orderLineId: number;
  productCode: string;
  color: string;
  sizeCode: string;
  issueQty: number;
  receivedQty: number | null;
};

export type DispatchNote = Omit<DispatchNoteListItem, 'totalIssueQty' | 'totalReceivedQty'> & {
  deliveryAddress: string;
  note: string | null;
  rejectReason: string | null;
  dispatchedByName: string | null;
  dispatchedAt: string | null;
  customerReceivedDate: string | null;
  lines: DispatchNoteLine[];
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
  lines: { dispatchNoteLineId: number; receivedQty: number }[];
};
