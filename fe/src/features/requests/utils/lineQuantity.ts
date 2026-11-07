import type { RequestType } from '@/constants/requestTypes';
import { MATERIAL_QTY_DECIMALS } from '@/constants/quantity';
import { FABRIC_MATERIAL_TYPE, FABRIC_QTY_DECIMALS, REQUEST_LINE_LIMITS } from '../constants';

export type LineQuantityRule = { decimals: number; min: number; max: number };

/**
 * Cách nhập số lượng của một dòng đơn xin (khớp RequisitionRequestHandlers ở BE):
 * Xin NVL: vải lẻ tối đa 2 chữ số, NVL khác vải số nguyên; Xin BTP: số nguyên; Cơ điện xin linh kiện, vật tư, máy: số nguyên có trần; loại khác: lẻ tối đa 3 chữ số.
 */
export const lineQuantityRule = (
  requestType: RequestType | undefined,
  materialType: string | undefined,
): LineQuantityRule => {
  if (requestType === 'SpareParts' || requestType === 'MachineSupplies')
    return { decimals: 0, min: 1, max: REQUEST_LINE_LIMITS.maxPartsQty };
  if (requestType === 'NewMachine')
    return { decimals: 0, min: 1, max: REQUEST_LINE_LIMITS.maxMachineQty };
  if (requestType === 'SemiFinishedRequisition')
    return { decimals: 0, min: 1, max: REQUEST_LINE_LIMITS.maxPieceQty };
  if (requestType === 'MaterialRequisition') {
    const isFabric = materialType === FABRIC_MATERIAL_TYPE;
    return isFabric
      ? { decimals: FABRIC_QTY_DECIMALS, min: 0.01, max: REQUEST_LINE_LIMITS.maxMaterialQty }
      : { decimals: 0, min: 1, max: REQUEST_LINE_LIMITS.maxMaterialQty };
  }
  return { decimals: MATERIAL_QTY_DECIMALS, min: 0.001, max: REQUEST_LINE_LIMITS.maxMaterialQty };
};

/** Đổi sang NVL không cho số lẻ thì làm tròn số đã nhập (tối thiểu 1). */
export const roundForMaterial = (quantity: number, materialType: string | undefined): number =>
  materialType === FABRIC_MATERIAL_TYPE ? quantity : Math.max(1, Math.round(quantity));
