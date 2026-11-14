/** Giới hạn của một phiếu cấp, khớp RequisitionLimits + CreateMaterialIssueDtoValidator ở BE. */
export const ISSUE_LIMITS = {
  maxLines: 50,
  maxMaterialQty: 9_999_999,
  maxPieceQty: 1_000_000,
  fabricDecimals: 2,
} as const;

export const FABRIC_MATERIAL_TYPE = 'FABRIC';

export type IssueQtyRule = { decimals: number; min: number; max: number };

/** Vải được lẻ tối đa 2 chữ số sau dấu phẩy; NVL khác (chỉ, cúc…) là số nguyên. Trần 9.999.999. */
export const materialIssueRule = (materialType: string | undefined): IssueQtyRule =>
  materialType === FABRIC_MATERIAL_TYPE
    ? { decimals: ISSUE_LIMITS.fabricDecimals, min: 0.01, max: ISSUE_LIMITS.maxMaterialQty }
    : { decimals: 0, min: 1, max: ISSUE_LIMITS.maxMaterialQty };

/** BTP đếm theo bộ: số nguyên từ 1 đến 1.000.000. */
export const PIECE_ISSUE_RULE: IssueQtyRule = {
  decimals: 0,
  min: 1,
  max: ISSUE_LIMITS.maxPieceQty,
};

/** Đổi sang NVL không cho số lẻ thì làm tròn số đã nhập (tối thiểu 1). */
export const roundIssueQty = (qty: number, materialType: string | undefined): number =>
  materialType === FABRIC_MATERIAL_TYPE ? qty : Math.max(1, Math.round(qty));
