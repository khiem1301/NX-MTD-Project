import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useApiMutation } from '@/hooks/useApiMutation';
import { gradeSuggestionApi } from '../api/gradeSuggestionApi';
import { AI_MESSAGES } from '../constants';
import type { GradeSuggestion, UpdateSuggestionItemPayload } from '../types';
import { GRADE_KEY } from './useGrade';

const SUGGESTION_KEY = [...GRADE_KEY, 'ai-suggestion'] as const;

/** DEV-ALL-148: bản gợi ý AI đang lưu tạm của tổ. */
export const useGradeSuggestion = (enabled: boolean) =>
  useQuery({ queryKey: SUGGESTION_KEY, queryFn: gradeSuggestionApi.get, enabled });

/** Kết quả tạo / sửa là bản gợi ý mới nhất: ghi thẳng vào cache để bảng đổi ngay, không cần tải lại. */
const useSetSuggestion = () => {
  const queryClient = useQueryClient();
  return (data: GradeSuggestion) => queryClient.setQueryData(SUGGESTION_KEY, data);
};

export const useGenerateSuggestion = () => {
  const setSuggestion = useSetSuggestion();
  const mutation = useApiMutation(gradeSuggestionApi.generate, {
    invalidate: [],
    successMessage: AI_MESSAGES.generated,
  });
  return {
    ...mutation,
    generate: (lookbackDays: number) => mutation.mutate(lookbackDays, { onSuccess: setSuggestion }),
  };
};

export const useUpdateSuggestionItem = () => {
  const setSuggestion = useSetSuggestion();
  const mutation = useApiMutation(
    ({ workerId, body }: { workerId: number; body: UpdateSuggestionItemPayload }) =>
      gradeSuggestionApi.updateItem(workerId, body),
    { invalidate: [] },
  );
  return {
    ...mutation,
    save: (workerId: number, body: UpdateSuggestionItemPayload) =>
      mutation.mutate({ workerId, body }, { onSuccess: setSuggestion }),
  };
};

export const useRemoveSuggestionItem = () => {
  const setSuggestion = useSetSuggestion();
  const mutation = useApiMutation(gradeSuggestionApi.removeItem, {
    invalidate: [],
    successMessage: AI_MESSAGES.removed,
  });
  return {
    ...mutation,
    remove: (workerId: number) => mutation.mutateAsync(workerId).then(setSuggestion),
  };
};

/** Xác nhận tạo / thay đề xuất bậc nên nạp lại danh sách đề xuất và bản gợi ý (có thời điểm xác nhận). */
export const useConfirmSuggestion = () =>
  useApiMutation(gradeSuggestionApi.confirm, { invalidate: [GRADE_KEY] });
