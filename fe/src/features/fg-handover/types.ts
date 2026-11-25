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

/** DEV-ALL-183: dòng đơn còn bàn giao của tổ may mà QA/QC thuộc về (gộp mọi đơn của tổ). qcQty = số QC đạt của tổ. */
export type FgTeamHandoverableLine = {
  orderId: number;
  orderCode: string;
  orderLineId: number;
  productId: number;
  productCode: string;
  color: string;
  sizeCode: string;
  qcQty: number;
  handedQty: number;
  remainingQty: number;
};

/** Kết quả bàn giao toàn bộ thành phẩm của tổ: mỗi đơn + mã hàng một phiếu. */
export type FgHandoverAllResult = {
  handoverCount: number;
  totalQty: number;
  handoverCodes: string[];
};

export type FgHandoverQuery = PageParams & { status?: string; orderId?: number };

export type ConfirmFgHandoverPayload = {
  lines: { fgHandoverLineId: number; confirmedQty: number; discrepancyReason?: string }[];
};
