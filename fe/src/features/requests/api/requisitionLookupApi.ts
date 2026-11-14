import { API } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse } from '@/types/api';
import type { RequisitionMaterialOption, RequisitionPieceOption } from '../types';

/** Hàng chọn được ở đơn Xin NVL / Xin BTP kèm số khả dụng trong kho (DEV-ALL-110). */
export const requisitionLookupApi = {
  /** orderId: chỉ NVL đã khai trong NVL của đơn hàng; không có thì NVL đang dùng trong danh mục. */
  materials: async (orderId?: number): Promise<RequisitionMaterialOption[]> =>
    (
      await apiClient.get<ApiResponse<RequisitionMaterialOption[]>>(
        API.lookups.requisitionMaterials,
        { params: { orderId } },
      )
    ).data.data,
  pieces: async (orderId: number): Promise<RequisitionPieceOption[]> =>
    (
      await apiClient.get<ApiResponse<RequisitionPieceOption[]>>(
        API.lookups.requisitionPieces(orderId),
      )
    ).data.data,
};
