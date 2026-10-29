// Lệnh cắt dùng ở cả feature cutting (lập, cắt, bàn giao) và warehouse (cấp vải theo lệnh).

export type WorkOrderListItem = {
  workOrderId: number;
  workOrderCode: string;
  orderId: number;
  orderCode: string;
  productId: number;
  productCode: string;
  cuttingTeamId: number;
  cuttingTeamName: string;
  dueDate: string;
  status: string;
  totalPlannedQty: number;
  createdAt: string;
};

/** Vải của lệnh cắt: đã cấp, khả dụng trong kho, tổ đã nhận / đã trả lại kho / thực dùng (không còn so với định mức). */
export type FabricRequirement = {
  materialId: number;
  materialCode: string;
  materialName: string;
  unit: string;
  materialColor: string;
  issuedQty: number;
  availableQty: number;
  receivedQty: number;
  returnedQty: number;
  usedQty: number;
};
