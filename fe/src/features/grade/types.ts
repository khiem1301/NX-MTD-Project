import type { PageParams } from '@/types/api';

export type GradeLevel = {
  gradeLevelId: number;
  gradeCode: string;
  gradeName: string;
  monthlyAmount: number;
  effectiveDate: string;
  isCurrent: boolean;
};

/** Bậc thợ trong danh mục (Admin thêm / sửa / ngừng dùng). inUse: đã có người / đề xuất / tiền bậc dùng nên không xóa được. */
export type GradeCode = {
  gradeCode: string;
  gradeName: string;
  sortOrder: number;
  status: 'Active' | 'Inactive';
  inUse: boolean;
};

export type SaveGradeCodePayload = { gradeCode: string; gradeName: string; sortOrder: number };
export type UpdateGradeCodePayload = { gradeName: string; sortOrder: number; status: 'Active' | 'Inactive' };
export type UpdateLevelPayload = { gradeName: string; monthlyAmount: number; effectiveDate: string };

export type GradeEvaluation = {
  gradeEvaluationId: number;
  workerId: number;
  workerCode: string;
  workerName: string;
  teamId: number | null;
  teamName: string | null;
  proposedByName: string;
  currentGradeCode: string | null;
  proposedGradeCode: string;
  reason: string;
  recentDefectCount: number;
  evaluationDate: string;
  status: string;
  effectiveDate: string | null;
  consideredAt: string | null;
  decisionNote: string | null;
  createdAt: string;
};

export type GradeWorker = {
  workerId: number;
  workerCode: string;
  fullName: string;
  currentGradeCode: string | null;
  recentDefectCount: number;
  hasPending: boolean;
  teamName: string | null;
  teamId?: number | null;
};

export type EvaluationQuery = PageParams & { status?: string };

export type SaveLevelPayload = {
  gradeCode: string;
  gradeName: string;
  monthlyAmount: number;
  effectiveDate: string;
};

export type ProposePayload = { workerId: number; proposedGradeCode: string; reason: string };
export type SetGradePayload = { workerId: number; gradeCode: string; reason: string; effectiveDate?: string };
export type ApplyPayload = { effectiveDate?: string; decisionNote?: string };
