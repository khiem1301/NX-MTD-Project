import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import type {
  ApplyPayload,
  EvaluationQuery,
  GradeEvaluation,
  GradeLevel,
  GradeWorker,
  ProposePayload,
  SaveLevelPayload,
  SetGradePayload,
} from '../types';

const get = async <T>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;
const post = async <T>(url: string, body?: unknown): Promise<T> =>
  (await apiClient.post<ApiResponse<T>>(url, body)).data.data;

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
};
