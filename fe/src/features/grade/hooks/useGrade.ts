import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { ROLES } from '@/constants/roles';
import { useApiMutation } from '@/hooks/useApiMutation';
import { useAuth } from '@/hooks/useAuth';
import { gradeApi } from '../api/gradeApi';
import { GR_MESSAGES } from '../constants';
import type {
  ApplyPayload,
  EvaluationQuery,
  ProposePayload,
  SaveGradeCodePayload,
  SaveLevelPayload,
  SetGradePayload,
  UpdateGradeCodePayload,
  UpdateLevelPayload,
} from '../types';

export const GRADE_KEY = ['grade'] as const;
// Áp dụng bậc đổi CurrentGradeCode của nhân viên nên nạp lại cả danh sách nhân viên.
const AFFECTED = [GRADE_KEY, ['users'], ['employees']];

const useBase = () => {
  const { user } = useAuth();
  if (user?.role === ROLES.Admin) return '/api/v1/admin/grade-evaluations';
  if (user?.role === ROLES.Manager) return '/api/v1/manager/grade-evaluations';
  return '/api/v1/team-leader/grade-evaluations';
};

export const useEvaluations = (query: EvaluationQuery) => {
  const base = useBase();
  return useQuery({
    queryKey: [...GRADE_KEY, 'evaluations', base, query],
    queryFn: () => gradeApi.evaluations(base, query),
    placeholderData: keepPreviousData,
  });
};

export const useGradeWorkers = (enabled: boolean) =>
  useQuery({ queryKey: [...GRADE_KEY, 'workers'], queryFn: gradeApi.workers, enabled });

/** Admin / Quản đốc: danh sách mọi công nhân để chọn khi đặt bậc trực tiếp. */
export const useAllGradeWorkers = (enabled: boolean) => {
  const base = useBase();
  return useQuery({
    queryKey: [...GRADE_KEY, 'all-workers', base],
    queryFn: () => gradeApi.allWorkers(base),
    enabled,
  });
};

export const useSetGrade = () => {
  const base = useBase();
  return useApiMutation((body: SetGradePayload) => gradeApi.setGrade(base, body), {
    invalidate: AFFECTED,
    successMessage: GR_MESSAGES.set,
  });
};

/** Bậc đang dùng dạng lựa chọn cho Select (đề xuất, đặt bậc). */
export const useGradeOptions = () => {
  const query = useQuery({
    queryKey: [...GRADE_KEY, 'code-options'],
    queryFn: gradeApi.codeOptions,
  });
  const options = (query.data ?? []).map((c) => ({
    value: c.gradeCode,
    label: c.gradeName || `Bậc ${c.gradeCode}`,
  }));
  return { ...query, options };
};

export const useGradeCodes = () =>
  useQuery({ queryKey: [...GRADE_KEY, 'codes'], queryFn: gradeApi.codes });

export const useCreateGradeCode = () =>
  useApiMutation((body: SaveGradeCodePayload) => gradeApi.createCode(body), {
    invalidate: [GRADE_KEY],
    successMessage: GR_MESSAGES.codeSaved,
  });

export const useUpdateGradeCode = () =>
  useApiMutation(
    ({ code, body }: { code: string; body: UpdateGradeCodePayload }) =>
      gradeApi.updateCode(code, body),
    {
      invalidate: [GRADE_KEY],
      successMessage: GR_MESSAGES.codeSaved,
    },
  );

export const useDeleteGradeCode = () =>
  useApiMutation((code: string) => gradeApi.deleteCode(code), {
    invalidate: [GRADE_KEY],
    successMessage: GR_MESSAGES.codeDeleted,
  });

export const useUpdateLevel = () =>
  useApiMutation(
    ({ id, body }: { id: number; body: UpdateLevelPayload }) => gradeApi.updateLevel(id, body),
    {
      invalidate: [GRADE_KEY],
      successMessage: GR_MESSAGES.levelSaved,
    },
  );

export const useDeleteLevel = () =>
  useApiMutation((id: number) => gradeApi.deleteLevel(id), {
    invalidate: [GRADE_KEY],
    successMessage: GR_MESSAGES.levelDeleted,
  });

export const useGradeLevels = () =>
  useQuery({ queryKey: [...GRADE_KEY, 'levels'], queryFn: gradeApi.levels });

export const useProposeGrade = () =>
  useApiMutation((body: ProposePayload) => gradeApi.propose(body), {
    invalidate: AFFECTED,
    successMessage: GR_MESSAGES.proposed,
  });

export const useApplyGrade = () => {
  const base = useBase();
  return useApiMutation(
    ({ id, body }: { id: number; body: ApplyPayload }) => gradeApi.apply(base, id, body),
    {
      invalidate: AFFECTED,
      successMessage: GR_MESSAGES.applied,
    },
  );
};

export const useRejectGrade = () => {
  const base = useBase();
  return useApiMutation(
    ({ id, reason }: { id: number; reason: string }) => gradeApi.reject(base, id, reason),
    {
      invalidate: AFFECTED,
      successMessage: GR_MESSAGES.notApplied,
    },
  );
};

export const useAddLevel = () =>
  useApiMutation((body: SaveLevelPayload) => gradeApi.addLevel(body), {
    invalidate: [GRADE_KEY],
    successMessage: GR_MESSAGES.levelSaved,
  });
