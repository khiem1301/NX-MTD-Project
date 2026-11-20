import { fill } from '@/utils/format';
import { MESSAGES } from '@/constants/messages';
import type { RequisitionItem, RequisitionMaterialOption, RequisitionPieceOption } from '../types';

export const fromMaterial = (m: RequisitionMaterialOption): RequisitionItem => ({
  id: m.materialId,
  label: `${m.materialCode} ${m.materialName}${m.color ? ` - ${m.color}` : ''}`,
  unit: m.unit,
  materialType: m.materialType,
  availableQty: m.availableQty,
});

export const fromPiece = (p: RequisitionPieceOption): RequisitionItem => ({
  id: p.orderLineId,
  label: fill(MESSAGES.requests.pieceLabel, p.productCode, p.color, p.sizeCode),
  unit: null,
  materialType: null,
  availableQty: p.availableQty,
});

/**
 * Trần của một dòng: số khả dụng trong kho (DEV-ALL-110), đơn theo lệnh cắt thì thêm số còn xin được của lệnh (DEV-ALL-92).
 * Trả về trần nhỏ nhất kèm câu báo lỗi; undefined khi chưa chọn hàng.
 */
export const lineCap = (
  item: RequisitionItem | undefined,
  remaining: number | undefined,
): { cap: number; message: string } | undefined => {
  if (!item) return undefined;
  const stock = Math.max(0, item.availableQty);
  return remaining !== undefined && remaining < stock
    ? { cap: remaining, message: MESSAGES.requests.overRemaining }
    : { cap: stock, message: MESSAGES.requests.overStock };
};
