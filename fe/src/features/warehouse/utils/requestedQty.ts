import type { RequestDetail } from '@/types/request';

/** Số lượng đơn xin theo NVL (phiếu cấp NVL) hoặc theo dòng đơn (phiếu cấp BTP): trần số cấp của từng dòng. */
export const requestedQtyMap = (request: RequestDetail | null, isMaterial: boolean) =>
  new Map<number, number>(
    (request?.lines ?? []).flatMap((l) => {
      const id = isMaterial ? l.materialId : l.orderLineId;
      return id === null ? [] : [[id, l.quantity] as [number, number]];
    }),
  );
