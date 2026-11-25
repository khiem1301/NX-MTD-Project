import { MOVEMENT_TYPE } from '../constants';

/** Số lần của phiếu thu hồi hiển thị âm (giao dương, thu hồi âm); 0 giữ nguyên là 0. */
export const signedQty = (movementType: string, qty: number): number =>
  movementType === MOVEMENT_TYPE.RETRIEVAL && qty > 0 ? -qty : qty;

/** Tổng thu hồi (số dương) hiển thị âm. */
export const negated = (qty: number): number => (qty > 0 ? -qty : qty);
