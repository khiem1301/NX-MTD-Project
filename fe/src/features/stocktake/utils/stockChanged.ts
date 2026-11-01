import type { StocktakeLine } from '../types';

/** Dòng mà tồn thực lúc xem khác số sổ lúc lập phiếu (chỉ phiếu chờ duyệt mới có tồn hiện tại). */
export const isStockChanged = (l: Pick<StocktakeLine, 'bookQty' | 'currentQty'>): boolean =>
  l.currentQty !== null && l.currentQty !== l.bookQty;
