/** NVL chọn được ở đơn Xin NVL kèm số khả dụng trong kho (trần số lượng xin, DEV-ALL-110). */
export type RequisitionMaterialOption = {
  materialId: number;
  materialCode: string;
  materialName: string;
  materialType: string;
  unit: string;
  color: string;
  availableQty: number;
};

/** Dòng đơn (cả bộ) chọn được ở đơn Xin BTP kèm số bộ khả dụng trong kho BTP (DEV-ALL-110). */
export type RequisitionPieceOption = {
  orderLineId: number;
  productCode: string;
  productName: string;
  color: string;
  sizeCode: string;
  orderedQty: number;
  availableQty: number;
};

/** Một hàng chọn được ở dòng đơn xin, dùng chung cho Xin NVL và Xin BTP. */
export type RequisitionItem = {
  id: number;
  label: string;
  unit: string | null;
  materialType: string | null;
  availableQty: number;
};
