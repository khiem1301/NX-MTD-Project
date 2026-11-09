import type { OrderLineOption } from '@/types/lookup';
import { pieceKey } from './pieceKey';

const FABRIC_DECIMALS = 3;

/** Làm tròn lên số lẻ của NVL để không cấp thiếu so với nhu cầu. */
export const roundUpQty = (value: number, decimals = FABRIC_DECIMALS): number => {
  const factor = 10 ** decimals;
  return Math.ceil(value * factor - 1e-9) / factor;
};

/** Đề xuất số vải cấp = nhu cầu theo màu / size đã chọn, không vượt số khả dụng trong kho. 0 nghĩa là chưa đề xuất được. */
export const suggestFabricQty = (needMeters: number, availableQty: number): number =>
  Math.max(0, Math.min(roundUpQty(needMeters), availableQty));

export type PieceSuggestionFilter = { productCode: string; color: string; sizeCode?: string };

export type PieceSuggestion = { pieceKey: string; issuedQty: number };

/**
 * Đề xuất dòng cấp BTP theo mã hàng + màu (+ size): mỗi dòng đơn khớp có số cấp = min(khả dụng trong kho BTP, số đặt),
 * bỏ dòng hết hàng và dòng đã có trong phiếu.
 */
export const suggestPieceLines = (
  lines: OrderLineOption[],
  available: ReadonlyMap<number, number>,
  filter: PieceSuggestionFilter,
  taken: ReadonlySet<string>,
): PieceSuggestion[] =>
  lines
    .filter(
      (l) =>
        l.productCode === filter.productCode &&
        l.color === filter.color &&
        (!filter.sizeCode || l.sizeCode === filter.sizeCode),
    )
    .map((l) => ({
      pieceKey: pieceKey(l.orderLineId, null),
      issuedQty: Math.min(available.get(l.orderLineId) ?? 0, l.orderedQty),
    }))
    .filter((s) => s.issuedQty > 0 && !taken.has(s.pieceKey));
