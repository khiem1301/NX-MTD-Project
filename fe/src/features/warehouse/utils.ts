/** Khóa một dòng BTP trong form: dòng đơn + chi tiết lẻ (trống = cả bộ). */
export const pieceKey = (orderLineId: number, productComponentId: number | null) =>
  `${orderLineId}:${productComponentId ?? ''}`;

export const parsePieceKey = (key: string) => {
  const [orderLineId, productComponentId] = key.split(':');
  return { orderLineId: Number(orderLineId), productComponentId: productComponentId ? Number(productComponentId) : null };
};
