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
  /** Vải tổ cắt đang giữ của đơn = đã nhận − đã trả về kho. */
  heldQty: number;
  /** Vải đã cắt của tổ trên đơn = tổng số lớp × chiều dài bàn cắt. */
  cutQty: number;
  /** Vải còn lại = heldQty − cutQty; âm nghĩa là đã cắt nhiều hơn số đang giữ. */
  remainingQty: number;
  /** Số bàn chưa có chiều dài bàn cắt nên chưa cộng vào cutQty. */
  unmeasuredLayCount: number;
};
