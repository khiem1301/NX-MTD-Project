import { API, type RolePath } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import type {
  AdminUpdatePayload,
  CreateEmployeePayload,
  CreatedEmployee,
  EmployeeDetail,
  EmployeeListItem,
  EmployeeQuery,
  ManagerUpdatePayload,
  TempPassword,
} from '../types';

const get = async <T>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;
const post = async <T>(url: string, body?: unknown): Promise<T> =>
  (await apiClient.post<ApiResponse<T>>(url, body)).data.data;
const put = async <T>(url: string, body: unknown): Promise<T> =>
  (await apiClient.put<ApiResponse<T>>(url, body)).data.data;

export const employeeApi = {
  list: (role: RolePath, q: EmployeeQuery) => get<Paginated<EmployeeListItem>>(API.employees.base(role), q),
  get: (role: RolePath, id: number) => get<EmployeeDetail>(API.employees.byId(role, id)),
  create: (role: RolePath, body: CreateEmployeePayload) => post<CreatedEmployee>(API.employees.base(role), body),
  adminUpdate: (id: number, body: AdminUpdatePayload) => put<unknown>(API.employees.byId('admin', id), body),
  managerUpdate: (id: number, body: ManagerUpdatePayload) => put<unknown>(API.employees.byId('manager', id), body),
  resign: (role: RolePath, id: number, body: { resignDate: string; reason: string; rowVer: string }) =>
    post<unknown>(API.employees.resign(role, id), body),
  lock: (role: RolePath, id: number, reason: string) => post<unknown>(API.employees.lock(role, id), { reason }),
  unlock: (role: RolePath, id: number) => post<unknown>(API.employees.unlock(role, id)),
  resetPassword: (role: RolePath, id: number) => post<TempPassword>(API.employees.resetPassword(role, id)),
};
