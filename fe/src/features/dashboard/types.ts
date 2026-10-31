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
