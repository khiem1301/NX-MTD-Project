/** Trần số lượng hàng chiếc mỗi ô (sản phẩm, chi tiết, suất ăn), khớp các *Limits.MaxQty = 1.000.000 ở BE. */
export const MAX_PIECE_QTY = 1_000_000;

/** Trần số lượng NVL mỗi ô, khớp OrderMaterialInputValidator.MaxQty = 100.000.000 ở BE. */
export const MAX_MATERIAL_QTY = 100_000_000;

/** Số chữ số lẻ của số lượng NVL, như cột DECIMAL(18,3). */
export const MATERIAL_QTY_DECIMALS = 3;
