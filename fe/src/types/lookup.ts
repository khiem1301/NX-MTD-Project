export type OrderOption = {
  orderId: number;
  orderCode: string;
  status: string;
  materialStatus: string;
  dueDate: string;
};

export type OrderLineOption = {
  orderLineId: number;
  productId: number;
  productCode: string;
  productName: string;
  color: string;
  sizeCode: string;
  orderedQty: number;
};

/** Chi tiết cắt (PANEL) của mã hàng, để chọn khi xin cắt bù. */
export type ComponentOption = { productComponentMaterialId: number; componentName: string; piecesPerGarment: number };

export type TeamOption = { teamId: number; teamCode: string; teamName: string; teamType: string };

export type MaterialOption = {
  materialId: number;
  materialCode: string;
  materialName: string;
  materialType: string;
  unit: string;
};
