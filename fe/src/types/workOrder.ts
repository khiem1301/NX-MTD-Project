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

/** Vải của lệnh cắt: cần theo định mức, đã cấp, còn phải cấp, khả dụng trong kho. */
export type FabricRequirement = {
  materialId: number;
  materialCode: string;
  materialName: string;
  unit: string;
  materialColor: string;
  requiredQty: number;
  issuedQty: number;
  remainingQty: number;
  availableQty: number;
  /** Kết quả vải: đã nhận, đã trả lại kho, thực dùng, vải theo định mức cho số đã cắt. */
  receivedQty: number;
  returnedQty: number;
  usedQty: number;
  cutNormQty: number;
};
