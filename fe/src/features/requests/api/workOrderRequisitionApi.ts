import { API } from '@/constants/apiEndpoints';
import { MAX_PAGE_SIZE } from '@/constants/pagination';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import type { WorkOrderListItem, WorkOrderMaterialAllowance } from '@/types/workOrder';

const ROLE = 'team-leader' as const;

/** Lệnh cắt của tổ cắt để gắn vào đơn xin NVL (DEV-ALL-92). */
export const workOrderRequisitionApi = {
  teamWorkOrders: async (): Promise<WorkOrderListItem[]> =>
    (
      await apiClient.get<ApiResponse<Paginated<WorkOrderListItem>>>(API.cutting.workOrders(ROLE), {
        params: { page: 1, pageSize: MAX_PAGE_SIZE },
      })
    ).data.data.items,
  allowance: async (
    workOrderId: number,
    exceptRequestId?: number,
  ): Promise<WorkOrderMaterialAllowance[]> =>
    (
      await apiClient.get<ApiResponse<WorkOrderMaterialAllowance[]>>(
        `${API.cutting.workOrders(ROLE)}/${workOrderId}/material-allowance`,
        { params: { exceptRequestId } },
      )
    ).data.data,
};
