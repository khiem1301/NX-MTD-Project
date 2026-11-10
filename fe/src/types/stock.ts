import type { PageParams } from '@/types/api';

// Khớp DTO tồn kho BE (Application/DTOs/Warehouse). Dùng chung cho kho và kiểm kê.

export type StockBalance = {
  warehouseType: string;
  materialId: number | null;
  materialCode: string | null;
  materialName: string | null;
  materialType: string | null;
  unit: string | null;
  materialColor: string | null;
  orderLineId: number | null;
  /** Chỉ kho BTP: null = bộ đủ; có giá trị = chi tiết lẻ (hàng cắt bù). */
  productComponentMaterialId: number | null;
  componentName: string | null;
  orderId: number | null;
  orderCode: string | null;
  productCode: string | null;
  productName: string | null;
  color: string | null;
  sizeCode: string | null;
  onHandQty: number;
  reservedQty: number;
  availableQty: number;
};

export type MaterialOrderScope = 'NO_ORDER';

export type StockBalanceQuery = PageParams & {
  warehouseType: string;
  orderId?: number;
  /** Chỉ kho NVL: NVL không thuộc đơn hàng nào (mua dự trữ dùng chung). */
  orderScope?: MaterialOrderScope;
  color?: string;
  sizeCode?: string;
};
