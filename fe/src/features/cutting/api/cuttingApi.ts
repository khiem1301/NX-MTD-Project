import { API, type RolePath } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import type { WorkOrderListItem } from '@/types/workOrder';
import type {
  FabricByOrderRow,
  FabricPreview,
  ConfirmHandoverPayload,
  CreateHandoverPayload,
  CutHandover,
  CutHandoverListItem,
  CutHandoverQuery,
  CreateReplacementPayload,
  CuttingLay,
  CuttingLayQuery,
  HandoverableLine,
  ReplacementRequest,
  SaveCuttingLayPayload,
  SaveWorkOrderPayload,
  WorkOrder,
  WorkOrderQuery,
} from '../types';

const get = async <T>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;
const post = async <T>(url: string, body?: unknown): Promise<ApiResponse<T>> =>
  (await apiClient.post<ApiResponse<T>>(url, body)).data;
const put = async <T>(url: string, body: unknown): Promise<ApiResponse<T>> =>
  (await apiClient.put<ApiResponse<T>>(url, body)).data;

export const cuttingApi = {
  getWorkOrders: (role: RolePath, q: WorkOrderQuery) =>
    get<Paginated<WorkOrderListItem>>(API.cutting.workOrders(role), q),
  getWorkOrder: (role: RolePath, id: number) =>
    get<WorkOrder>(`${API.cutting.workOrders(role)}/${id}`),
  createWorkOrder: (role: RolePath, body: SaveWorkOrderPayload) =>
    post<WorkOrder>(API.cutting.workOrders(role), body),
  updateWorkOrder: (role: RolePath, id: number, body: SaveWorkOrderPayload) =>
    put<WorkOrder>(`${API.cutting.workOrders(role)}/${id}`, body),
  previewFabric: async (role: RolePath, body: SaveWorkOrderPayload, exceptWorkOrderId?: number) =>
    (await post<FabricPreview>(`${API.cutting.workOrders(role)}/fabric-preview${exceptWorkOrderId ? `?exceptWorkOrderId=${exceptWorkOrderId}` : ''}`, body)).data,
  getFabricByOrder: (role: RolePath, orderId?: number) =>
    get<FabricByOrderRow[]>(`${API.cutting.workOrders(role)}/fabric-by-order`, { orderId }),
  getReplacementRequests: (role: RolePath) =>
    get<ReplacementRequest[]>(`${API.cutting.workOrders(role)}/replacement-requests`),
  createReplacement: (role: RolePath, body: CreateReplacementPayload) =>
    post<WorkOrder>(`${API.cutting.workOrders(role)}/replacement`, body),
  finishCutting: (role: RolePath, id: number, reason?: string) =>
    post<null>(`${API.cutting.workOrders(role)}/${id}/finish-cutting`, { reason: reason?.trim() || null }),
  cancelWorkOrder: (role: RolePath, id: number) =>
    post<null>(`${API.cutting.workOrders(role)}/${id}/cancel`),

  getLays: (role: RolePath, q: CuttingLayQuery) =>
    get<Paginated<CuttingLay>>(API.cutting.cuttingLays(role), q),
  /** Trả cả ApiResponse vì message có thể là cảnh báo "vượt kế hoạch trong % dư". */
  createLay: (role: RolePath, body: SaveCuttingLayPayload) =>
    post<CuttingLay>(API.cutting.cuttingLays(role), body),
  updateLay: (role: RolePath, id: number, body: SaveCuttingLayPayload) =>
    put<CuttingLay>(`${API.cutting.cuttingLays(role)}/${id}`, body),

  getHandovers: (role: RolePath, q: CutHandoverQuery) =>
    get<Paginated<CutHandoverListItem>>(API.cutting.cutHandovers(role), q),
  getHandover: (role: RolePath, id: number) =>
    get<CutHandover>(`${API.cutting.cutHandovers(role)}/${id}`),
  getHandoverable: (role: RolePath, workOrderId: number) =>
    get<HandoverableLine[]>(`${API.cutting.cutHandovers(role)}/handoverable`, { workOrderId }),
  createHandover: (role: RolePath, body: CreateHandoverPayload) =>
    post<CutHandover>(API.cutting.cutHandovers(role), body),
  confirmHandover: (role: RolePath, id: number, body: ConfirmHandoverPayload) =>
    post<null>(`${API.cutting.cutHandovers(role)}/${id}/confirm`, body),
  rejectHandover: (role: RolePath, id: number, reason: string) =>
    post<null>(`${API.cutting.cutHandovers(role)}/${id}/reject`, { reason }),
  cancelHandover: (role: RolePath, id: number) => post<null>(`${API.cutting.cutHandovers(role)}/${id}/cancel`),
};
