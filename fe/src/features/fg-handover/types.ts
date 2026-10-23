import type { PageParams } from '@/types/api';

export type FgHandoverListItem = {
  fgHandoverId: number;
  handoverCode: string;
  orderId: number;
  orderCode: string;
  productId: number;
  productCode: string;
  inspectorId: number;
  inspectorName: string;
  handedAt: string;
  status: string;
  confirmedByName: string | null;
  confirmedAt: string | null;
  totalHandedQty: number;
};

export type FgHandoverLine = {
  fgHandoverLineId: number;
  orderLineId: number;
  color: string;
  sizeCode: string;
  handedQty: number;
  confirmedQty: number | null;
  discrepancyReason: string | null;
};

export type FgHandover = Omit<FgHandoverListItem, 'totalHandedQty'> & {
  rejectReason: string | null;
  lines: FgHandoverLine[];
};

export type FgHandoverableLine = {
  orderLineId: number;
  productId: number;
  productCode: string;
  color: string;
  sizeCode: string;
  qcQty: number;
  handedQty: number;
  remainingQty: number;
};

export type FgHandoverQuery = PageParams & { status?: string; orderId?: number };

export type CreateFgHandoverPayload = {
  orderId: number;
  productId: number;
  lines: { orderLineId: number; handedQty: number }[];
};

export type ConfirmFgHandoverPayload = {
  lines: { fgHandoverLineId: number; confirmedQty: number; discrepancyReason?: string }[];
};
