import type { PageParams } from '@/types/api';

export type GradeLevel = {
  gradeLevelId: number;
  gradeCode: string;
  monthlyAmount: number;
  effectiveDate: string;
  isCurrent: boolean;
};

/** Bậc thợ trong danh mục (Admin thêm / sửa / ngừng dùng). inUse: đã có người / đề xuất / tiền bậc dùng nên không xóa được. */
export type GradeCode = {
  gradeCode: string;
  status: 'Active' | 'Inactive';
  inUse: boolean;
};

export type SaveGradeCodePayload = { gradeCode: string };
export type UpdateGradeCodePayload = { status: 'Active' | 'Inactive' };
export type UpdateLevelPayload = {
  monthlyAmount: number;
  effectiveDate: string;
};

export type GradeEvaluation = {
  gradeEvaluationId: number;
  workerId: number;
  workerCode: string;
  workerName: string;
  teamId: number | null;
  teamCode: string | null;
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
  teamCode: string | null;
  teamId?: number | null;
};

export type EvaluationQuery = PageParams & { status?: string };

export type SaveLevelPayload = {
  gradeCode: string;
  monthlyAmount: number;
  effectiveDate: string;
};

export type ProposePayload = { workerId: number; proposedGradeCode: string; reason: string };
export type SetGradePayload = {
  workerId: number;
  gradeCode: string;
  reason: string;
  effectiveDate?: string;
};
export type ApplyPayload = { effectiveDate?: string; decisionNote?: string };

// ---------- AI gợi ý bậc cho cả tổ (DEV-ALL-148) ----------

/** Số liệu kỳ xét bậc của một công nhân (không có tiền). */
export type GradeWorkerMetrics = {
  tenureMonths: number;
  workDays: number;
  workdayTotal: number;
  workedHours: number;
  overtimeHours: number;
  outputQty: number;
  operationCount: number;
  avgSkillLevel: number | null;
  efficiencyPercent: number | null;
  reworkQty: number;
  scrapQty: number;
  defectRatePercent: number | null;
};

/** aiGradeCode / aiReason: bản AI trả; proposedGradeCode / reason: bản sẽ gửi (đổi khi tổ trưởng sửa). */
export type GradeSuggestionItem = {
  workerId: number;
  workerCode: string;
  fullName: string;
  currentGradeCode: string | null;
  pendingGradeCode: string | null;
  metrics: GradeWorkerMetrics;
  aiGradeCode: string | null;
  aiReason: string;
  proposedGradeCode: string | null;
  reason: string;
  isEdited: boolean;
};

export type GradeSuggestion = {
  teamId: number;
  periodFrom: string;
  periodTo: string;
  generatedAt: string;
  expiresAt: string;
  generatedByName: string;
  confirmedAt: string | null;
  items: GradeSuggestionItem[];
};

export type UpdateSuggestionItemPayload = { proposedGradeCode: string; reason: string };

export type GradeSuggestionConfirmResult = {
  created: number;
  replaced: number;
  unchanged: number;
  skipped: string[];
};
