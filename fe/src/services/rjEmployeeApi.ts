import { API, type RolePath } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse } from '@/types/api';
import type { TempPassword } from '@/types/auth';
import type { ImportRjEmployeePayload, RjEmployee } from '@/types/rjEmployee';

type CreatedEmployee = TempPassword & { employeeId: number };

/** NV từ Ronald Jack Pro: dùng chung cho chấm công và nhân viên. */
export const rjEmployeeApi = {
  /** Admin / Quản đốc / Kế toán (GET /api/v1/common/rj-employees). */
  listCommon: async (onlyUnmapped = false): Promise<RjEmployee[]> =>
    (
      await apiClient.get<ApiResponse<RjEmployee[]>>(API.rjEmployees, {
        params: { onlyUnmapped },
      })
    ).data.data,
  /** Tạo công nhân từ NV trên RJ: chỉ Admin / Quản đốc. */
  importToEmployee: async (
    role: RolePath,
    body: ImportRjEmployeePayload,
  ): Promise<CreatedEmployee> =>
    (await apiClient.post<ApiResponse<CreatedEmployee>>(API.employees.fromRj(role), body)).data
      .data,
};
