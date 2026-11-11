import { useQuery } from '@tanstack/react-query';
import { REQUESTS_KEY } from '@/hooks/useRequests';
import { workOrderRequisitionApi } from '../api/workOrderRequisitionApi';
import { CUTTABLE_WORK_ORDER_STATUSES } from '../constants';

/** Lệnh cắt đang mở / đang cắt của tổ cắt (chỉ tải khi form đơn xin NVL của tổ cắt mở). */
export const useTeamWorkOrders = (enabled: boolean) =>
  useQuery({
    queryKey: [...REQUESTS_KEY, 'team-work-orders'],
    queryFn: workOrderRequisitionApi.teamWorkOrders,
    enabled,
    select: (items) => items.filter((w) => CUTTABLE_WORK_ORDER_STATUSES.includes(w.status)),
  });

/** Vải hệ thống tính, trần, đã xin, còn xin được của lệnh; sửa đơn thì không tính số của chính đơn đó. */
export const useWorkOrderAllowance = (workOrderId: number | undefined, exceptRequestId?: number) =>
  useQuery({
    queryKey: [...REQUESTS_KEY, 'work-order-allowance', workOrderId, exceptRequestId],
    queryFn: () => workOrderRequisitionApi.allowance(workOrderId ?? 0, exceptRequestId),
    enabled: workOrderId !== undefined,
  });
