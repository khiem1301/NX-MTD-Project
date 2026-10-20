import { API, type RolePath } from '@/constants/apiEndpoints';
import { MAX_PAGE_SIZE } from '@/constants/pagination';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import type { FabricRequirement, WorkOrderListItem } from '@/types/workOrder';
import type {
  ConfirmIssuePayload,
  CreateIssuePayload,
  Material,
  MaterialIssue,
  MaterialIssueListItem,
  MaterialIssueQuery,
  MaterialQuery,
  MaterialReceipt,
  MaterialReceiptListItem,
  MaterialReceiptQuery,
  OrderMaterial,
  SaveMaterialPayload,
  SaveOrderMaterialPayload,
  SaveReceiptPayload,
  StockBalance,
  StockBalanceQuery,
  StockCardEntry,
  StockCardQuery,
  TeamPieceHolding,
} from '../types';

const get = async <T>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;
const post = async <T>(url: string, body?: unknown): Promise<T> =>
  (await apiClient.post<ApiResponse<T>>(url, body)).data.data;
const put = async <T>(url: string, body: unknown): Promise<T> =>
  (await apiClient.put<ApiResponse<T>>(url, body)).data.data;

export const warehouseApi = {
  getMaterials: (role: RolePath, q: MaterialQuery) =>
    get<Paginated<Material>>(API.warehouse.materials(role), q),
  createMaterial: (role: RolePath, body: SaveMaterialPayload) =>
    post<Material>(API.warehouse.materials(role), body),
  updateMaterial: (role: RolePath, id: number, body: SaveMaterialPayload) =>
    put<Material>(`${API.warehouse.materials(role)}/${id}`, body),
  changeMaterialStatus: async (role: RolePath, id: number, status: string): Promise<void> => {
    await apiClient.patch(`${API.warehouse.materials(role)}/${id}/status`, { status });
  },

  getOrderMaterials: (role: RolePath, orderId: number) =>
    get<OrderMaterial[]>(API.warehouse.orderMaterials(role), { orderId }),
  createOrderMaterial: (role: RolePath, body: SaveOrderMaterialPayload) =>
    post<OrderMaterial>(API.warehouse.orderMaterials(role), body),
  updateOrderMaterial: (role: RolePath, id: number, body: SaveOrderMaterialPayload) =>
    put<OrderMaterial>(`${API.warehouse.orderMaterials(role)}/${id}`, body),
  deleteOrderMaterial: async (role: RolePath, id: number): Promise<void> => {
    await apiClient.delete(`${API.warehouse.orderMaterials(role)}/${id}`);
  },

  getReceipts: (role: RolePath, q: MaterialReceiptQuery) =>
    get<Paginated<MaterialReceiptListItem>>(API.warehouse.materialReceipts(role), q),
  getReceipt: (role: RolePath, id: number) =>
    get<MaterialReceipt>(`${API.warehouse.materialReceipts(role)}/${id}`),
  createReceipt: (role: RolePath, body: SaveReceiptPayload) =>
    post<MaterialReceipt>(API.warehouse.materialReceipts(role), body),
  updateReceipt: (role: RolePath, id: number, body: SaveReceiptPayload) =>
    put<MaterialReceipt>(`${API.warehouse.materialReceipts(role)}/${id}`, body),
  confirmReceipt: (role: RolePath, id: number) =>
    post<null>(`${API.warehouse.materialReceipts(role)}/${id}/confirm`),
  returnReceipt: (role: RolePath, id: number, reason: string) =>
    post<null>(`${API.warehouse.materialReceipts(role)}/${id}/return`, { reason }),

  getIssues: (role: RolePath, q: MaterialIssueQuery) =>
    get<Paginated<MaterialIssueListItem>>(API.warehouse.materialIssues(role), q),
  getIssue: (role: RolePath, id: number) =>
    get<MaterialIssue>(`${API.warehouse.materialIssues(role)}/${id}`),
  createIssue: (role: RolePath, body: CreateIssuePayload) =>
    post<MaterialIssue>(API.warehouse.materialIssues(role), body),
  confirmIssue: (role: RolePath, id: number, body: ConfirmIssuePayload) =>
    post<null>(`${API.warehouse.materialIssues(role)}/${id}/confirm`, body),
  rejectIssue: (role: RolePath, id: number, reason: string) =>
    post<null>(`${API.warehouse.materialIssues(role)}/${id}/reject`, { reason }),

  /** Lệnh cắt gần nhất để Thủ kho chọn khi cấp vải (UC40); lọc trạng thái ở hook. */
  getRecentWorkOrders: (role: RolePath) =>
    get<Paginated<WorkOrderListItem>>(API.cutting.workOrders(role), { pageSize: MAX_PAGE_SIZE }),
  getWorkOrderFabrics: (role: RolePath, workOrderId: number) =>
    get<FabricRequirement[]>(`${API.cutting.workOrders(role)}/${workOrderId}/fabrics`),

  getBalances: (role: RolePath, q: StockBalanceQuery) =>
    get<Paginated<StockBalance>>(API.warehouse.inventories(role), q),
  getStockCard: (role: RolePath, q: StockCardQuery) =>
    get<StockCardEntry[]>(`${API.warehouse.inventories(role)}/stock-card`, q),
  getTeamHoldings: (role: RolePath, orderId?: number) =>
    role === 'team-leader'
      ? get<TeamPieceHolding[]>(API.warehouse.pieceHoldings(role), { orderId })
      : get<TeamPieceHolding[]>(`${API.warehouse.inventories(role)}/team-holdings`, { orderId }),
};
