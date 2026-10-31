/** Khóa một dòng BTP trong form: dòng đơn + chi tiết lẻ (trống = cả bộ). */
export const pieceKey = (orderLineId: number, productComponentMaterialId: number | null) =>
  `${orderLineId}:${productComponentMaterialId ?? ''}`;

export const parsePieceKey = (key: string) => {
  const [orderLineId, productComponentMaterialId] = key.split(':');
  return {
    orderLineId: Number(orderLineId),
    productComponentMaterialId: productComponentMaterialId
      ? Number(productComponentMaterialId)
      : null,
  };
};
