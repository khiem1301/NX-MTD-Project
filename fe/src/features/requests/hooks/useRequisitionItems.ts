import { useQuery } from '@tanstack/react-query';
import { REQUESTS_KEY } from '@/hooks/useRequests';
import type { RequestType } from '@/constants/requestTypes';
import { requisitionLookupApi } from '../api/requisitionLookupApi';
import { fromMaterial, fromPiece } from '../utils/requisitionItems';

/**
 * Hàng chọn được ở dòng đơn xin kèm số khả dụng trong kho (DEV-ALL-110). Xin BTP: dòng đơn của đơn hàng (tổ may chỉ thấy dòng
 * chia cho tổ). Xin NVL: có đơn hàng thì NVL của đơn, không thì NVL đang dùng. needsOrder: chưa chọn đơn hàng thì chưa tải.
 */
export const useRequisitionItems = (
  requestType: RequestType | undefined,
  orderId: number | undefined,
  needsOrder: boolean,
) => {
  const isPiece = requestType === 'SemiFinishedRequisition';
  const isMaterial = requestType === 'MaterialRequisition';
  const ready = !needsOrder || orderId !== undefined;
  const materials = useQuery({
    queryKey: [...REQUESTS_KEY, 'requisition-materials', orderId ?? null],
    queryFn: () => requisitionLookupApi.materials(orderId),
    enabled: isMaterial && ready,
    select: (rows) => rows.map(fromMaterial),
  });
  const pieces = useQuery({
    queryKey: [...REQUESTS_KEY, 'requisition-pieces', orderId],
    queryFn: () => requisitionLookupApi.pieces(orderId ?? 0),
    enabled: isPiece && orderId !== undefined,
    select: (rows) => rows.map(fromPiece),
  });
  const active = isPiece ? pieces : materials;
  return { items: active.data ?? [], isLoading: active.isLoading && ready, ready };
};
