import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import type {
  ConfirmFgHandoverPayload,
  FgHandover,
  FgHandoverAllResult,
  FgHandoverListItem,
  FgHandoverQuery,
  FgTeamHandoverableLine,
} from '../types';

const get = async <T>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;
const post = async <T>(url: string, body?: unknown): Promise<T> =>
  (await apiClient.post<ApiResponse<T>>(url, body)).data.data;

export const QA_QC_BASE = '/api/v1/qa-qc/fg-handovers';
export const WAREHOUSE_BASE = '/api/v1/warehouse-keeper/fg-handovers';

/** QA/QC và Thủ kho dùng hai nhóm URL khác nhau; base do hook chọn theo vai trò đăng nhập. */
export const fgHandoverApi = {
  list: (base: string, q: FgHandoverQuery) => get<Paginated<FgHandoverListItem>>(base, q),
  detail: (base: string, id: number) => get<FgHandover>(`${base}/${id}`),
  /** Thành phẩm còn bàn giao của tổ may mà QA/QC thuộc về (BE lấy tổ theo tài khoản). */
  teamHandoverable: () => get<FgTeamHandoverableLine[]>(`${QA_QC_BASE}/team-handoverable`),
  handoverAll: () => post<FgHandoverAllResult>(`${QA_QC_BASE}/handover-all`),
  cancel: (id: number) => post<null>(`${QA_QC_BASE}/${id}/cancel`),
  confirm: (id: number, body: ConfirmFgHandoverPayload) =>
    post<null>(`${WAREHOUSE_BASE}/${id}/confirm`, body),
  reject: (id: number, reason: string) => post<null>(`${WAREHOUSE_BASE}/${id}/reject`, { reason }),
};
