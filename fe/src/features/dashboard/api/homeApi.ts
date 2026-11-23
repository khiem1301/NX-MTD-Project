import { apiClient } from '@/services/apiClient';
import type { ApiResponse } from '@/types/api';
import type {
  AccountantHome,
  AdminHome,
  ElectricianHome,
  ManagerHome,
  QaqcHome,
  TeamLeaderHome,
  WarehouseHome,
  WorkerHome,
} from '../types';

const get = async <T>(url: string): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url)).data.data;

/** Trang chủ theo vai trò: mỗi role một API (DEV-ALL-168). */
export const homeApi = {
  admin: () => get<AdminHome>('/api/v1/admin/home'),
  manager: () => get<ManagerHome>('/api/v1/manager/home'),
  accountant: () => get<AccountantHome>('/api/v1/accountant/home'),
  warehouse: () => get<WarehouseHome>('/api/v1/warehouse-keeper/home'),
  teamLeader: () => get<TeamLeaderHome>('/api/v1/team-leader/home'),
  qaqc: () => get<QaqcHome>('/api/v1/qa-qc/home'),
  worker: () => get<WorkerHome>('/api/v1/worker/home'),
  electrician: () => get<ElectricianHome>('/api/v1/electrician/home'),
};
