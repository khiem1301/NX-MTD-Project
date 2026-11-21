import { useAuth } from '@/hooks/useAuth';
import { useTeamMemberLookup } from '@/hooks/useHrLookups';
import { fill } from '@/utils/format';
import { PRODUCTION_TEXT } from '../constants';

const text = PRODUCTION_TEXT.assignments;

/** Danh sách công nhân đang làm của tổ mình (kèm bậc thợ), dùng cho các ô chọn công nhân. */
export function useWorkerOptions() {
  const { user } = useAuth();
  const members = useTeamMemberLookup(user?.teamId ?? null);
  const options = (members.data ?? [])
    .filter((m) => m.role === 'Worker')
    .map((m) => ({
      value: m.employeeId,
      label: m.currentGradeCode
        ? fill(text.workerLabelWithGrade, m.employeeCode, m.fullName, m.currentGradeCode)
        : fill(text.workerLabel, m.employeeCode, m.fullName),
    }));
  return { options, loading: members.isLoading };
}
