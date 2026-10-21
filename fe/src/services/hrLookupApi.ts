import { API } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse } from '@/types/api';
import type { EmployeeLookup, TeamLookup } from '@/types/hrLookup';

/** GET /api/v1/common/hr-lookups/... (mọi vai trò trừ Công nhân). Tổ trưởng / tổ phó chỉ tra tổ mình. */
export const hrLookupApi = {
  teams: async (): Promise<TeamLookup[]> =>
    (await apiClient.get<ApiResponse<TeamLookup[]>>(API.hrLookups.teams)).data.data,
  teamMembers: async (teamId: number): Promise<EmployeeLookup[]> =>
    (await apiClient.get<ApiResponse<EmployeeLookup[]>>(API.hrLookups.teamMembers, { params: { teamId } })).data.data,
};
