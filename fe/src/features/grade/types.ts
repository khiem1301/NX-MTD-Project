import type { PageParams } from '@/types/api';

export type GradeLevel = {
  gradeLevelId: number;
  gradeCode: string;
  gradeName: string;
  monthlyAmount: number;
  effectiveDate: string;
  isCurrent: boolean;
};

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
