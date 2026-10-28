import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import type {
  ApplyPayload,
  EvaluationQuery,
  GradeCode,
  GradeEvaluation,
  GradeLevel,
  GradeWorker,
  ProposePayload,
  SaveGradeCodePayload,
  SaveLevelPayload,
  SetGradePayload,
  UpdateGradeCodePayload,
  UpdateLevelPayload,
} from '../types';

const get = async <T>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;
const post = async <T>(url: string, body?: unknown): Promise<T> =>
  (await apiClient.post<ApiResponse<T>>(url, body)).data.data;
const put = async <T>(url: string, body: unknown): Promise<T> =>
  (await apiClient.put<ApiResponse<T>>(url, body)).data.data;
const del = async (url: string): Promise<void> => {
  await apiClient.delete(url);
};

/** Tổ trưởng dùng nhóm team-leader; Admin / Quản đốc dùng nhóm của mình; mức tiền bậc chỉ Admin. */
export const gradeApi = {
  evaluations: (base: string, q: EvaluationQuery) => get<Paginated<GradeEvaluation>>(base, q),
  workers: () => get<GradeWorker[]>('/api/v1/team-leader/grade-evaluations/workers'),
  /** Admin / Quản đốc: mọi công nhân đang làm việc, để đặt bậc trực tiếp. */
  allWorkers: (base: string) => get<GradeWorker[]>(`${base}/workers`),
  setGrade: (base: string, body: SetGradePayload) => post<null>(`${base}/set`, body),
  propose: (body: ProposePayload) =>
    post<GradeEvaluation>('/api/v1/team-leader/grade-evaluations', body),
  apply: (base: string, id: number, body: ApplyPayload) => post<null>(`${base}/${id}/apply`, body),
  reject: (base: string, id: number, reason: string) =>
    post<null>(`${base}/${id}/reject`, { reason }),
  levels: () => get<GradeLevel[]>('/api/v1/admin/grade-levels'),
  addLevel: (body: SaveLevelPayload) => post<GradeLevel>('/api/v1/admin/grade-levels', body),
  updateLevel: (id: number, body: UpdateLevelPayload) => put<GradeLevel>(`/api/v1/admin/grade-levels/${id}`, body),
  deleteLevel: (id: number) => del(`/api/v1/admin/grade-levels/${id}`),
  /** Các bậc đang dùng, để chọn khi đề xuất / đặt bậc. */
  codeOptions: () => get<GradeCode[]>('/api/v1/common/grade-code-options'),
  /** Admin: toàn bộ danh mục bậc (kể cả ngừng dùng). */
  codes: () => get<GradeCode[]>('/api/v1/admin/grade-codes'),
  createCode: (body: SaveGradeCodePayload) => post<GradeCode>('/api/v1/admin/grade-codes', body),
  updateCode: (code: string, body: UpdateGradeCodePayload) => put<GradeCode>(`/api/v1/admin/grade-codes/${code}`, body),
  deleteCode: (code: string) => del(`/api/v1/admin/grade-codes/${code}`),
};
