import type { PurchaseOrderDetail } from '../types';

/** Mốc trả đủ của đơn mua: Tổng giá trị thực tế (đơn Chốt không bù, DEV-ALL-129) hoặc tổng giá trị đơn. */
export const payableAmount = (order: PurchaseOrderDetail): number =>
  order.goodsStatus === 'ClosedShort' && order.actualAmount !== null
    ? order.actualAmount
    : order.totalAmount;
