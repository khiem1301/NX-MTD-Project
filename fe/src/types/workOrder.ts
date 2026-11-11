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

/**
 * Vải của lệnh cắt cho đơn xin NVL theo lệnh (DEV-ALL-92): cần = số mét hệ thống tính, trần = cần × (1 + tỉ lệ sai lệch),
 * đã xin ở các đơn khác của lệnh, còn xin được. needQty null = chưa tính được, không xin theo lệnh được.
 */
export type WorkOrderMaterialAllowance = {
  materialId: number;
  materialCode: string;
  materialName: string;
  materialColor: string;
  unit: string;
  needQty: number | null;
  capQty: number | null;
  requestedQty: number;
  remainingQty: number | null;
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
  /** Vải đã cắt của tổ trên đơn = tổng số mét vải đã dùng của các bàn. */
  cutQty: number;
  /** Vải còn lại = heldQty − cutQty; âm nghĩa là đã cắt nhiều hơn số đang giữ. */
  remainingQty: number;
  /** Số bàn cũ chưa có số mét vải đã dùng nên chưa cộng vào cutQty. */
  unmeasuredLayCount: number;
};
