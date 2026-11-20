import { apiClient } from '@/services/apiClient';
import type { ApiResponse } from '@/types/api';
import type {
  GradeSuggestion,
  GradeSuggestionConfirmResult,
  UpdateSuggestionItemPayload,
} from '../types';

const BASE = '/api/v1/team-leader/grade-suggestions';

/** DEV-ALL-148: tổ trưởng / tổ phó dùng AI gợi ý bậc cho cả tổ. */
export const gradeSuggestionApi = {
  /** Bản gợi ý còn hạn của tổ, null nếu chưa có / đã hết hạn. */
  get: async () => (await apiClient.get<ApiResponse<GradeSuggestion | null>>(BASE)).data.data,
  /** lookbackDays: số ngày gần nhất lấy số liệu, tổ trưởng chọn. */
  generate: async (lookbackDays: number) =>
    (await apiClient.post<ApiResponse<GradeSuggestion>>(`${BASE}/generate`, { lookbackDays })).data
      .data,
  updateItem: async (workerId: number, body: UpdateSuggestionItemPayload) =>
    (await apiClient.put<ApiResponse<GradeSuggestion>>(`${BASE}/items/${workerId}`, body)).data
      .data,
  /** Bỏ dòng gợi ý của một công nhân khỏi bản gợi ý. */
  removeItem: async (workerId: number) =>
    (await apiClient.delete<ApiResponse<GradeSuggestion>>(`${BASE}/items/${workerId}`)).data.data,
  confirm: async () =>
    (await apiClient.post<ApiResponse<GradeSuggestionConfirmResult>>(`${BASE}/confirm`)).data.data,
};
