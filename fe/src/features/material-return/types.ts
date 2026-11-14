import type { PageParams } from '@/types/api';

/** Kho nhận hàng trả: NVL / phế phẩm (MATERIAL) hoặc BTP (SEMI_FINISHED). */
export type ReturnWarehouse = 'MATERIAL' | 'SEMI_FINISHED';

export type MaterialReturnListItem = {
  materialReturnId: number;
  returnCode: string;
  warehouseType: ReturnWarehouse;
  teamId: number;
  teamName: string;
  orderCode: string | null;
  workOrderCode: string | null;
  status: string;
  createdAt: string;
  confirmedByName: string | null;
  confirmedAt: string | null;
  totalReturnedQty: number;
};

/** NVL: itemCode / itemName là mã và tên NVL; BTP: mã hàng và "màu/size" (đơn vị bộ). */
export type MaterialReturnLine = {
  materialReturnLineId: number;
  materialId: number | null;
  orderLineId: number | null;
  itemCode: string;
  itemName: string;
  unit: string;
  itemColor: string;
  isScrap: boolean;
  returnedQty: number;
  confirmedQty: number | null;
  discrepancyReason: string | null;
};

export type MaterialReturn = Omit<MaterialReturnListItem, 'totalReturnedQty'> & {
  note: string | null;
  rejectReason: string | null;
  lines: MaterialReturnLine[];
};

/**
 * Hàng tổ còn trả được. usedQty: tổ cắt = số mét đã cắt của lệnh (null khi có bàn cắt chưa nhập số mét), tổ may trả BTP = số đã
 * QC đạt, tổ may trả NVL = null. remainingQty là trần số trả.
 */
export type ReturnableLine = {
  materialId: number | null;
  orderLineId: number | null;
  itemCode: string;
  itemName: string;
  unit: string;
  itemColor: string;
  receivedQty: number;
  usedQty: number | null;
  returnedQty: number;
  remainingQty: number;
};

export type ReturnableQuery = {
  warehouseType: ReturnWarehouse;
  workOrderId?: number;
  orderId?: number;
};

export type MaterialReturnQuery = PageParams & { status?: string };

export type CreateReturnPayload = {
  warehouseType: ReturnWarehouse;
  workOrderId?: number;
  orderId?: number;
  note?: string;
  lines: { materialId?: number; orderLineId?: number; returnedQty: number }[];
};

export type ConfirmReturnPayload = {
  lines: { materialReturnLineId: number; confirmedQty: number; discrepancyReason?: string }[];
};
