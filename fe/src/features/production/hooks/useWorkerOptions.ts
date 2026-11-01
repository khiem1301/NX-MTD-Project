import { useAuth } from '@/hooks/useAuth';
import { useTeamMemberLookup } from '@/hooks/useHrLookups';

/** Danh sách công nhân đang làm của tổ mình, dùng cho các ô chọn công nhân. */
export function useWorkerOptions() {
  const { user } = useAuth();
  const members = useTeamMemberLookup(user?.teamId ?? null);
  const options = (members.data ?? [])
    .filter((m) => m.role === 'Worker')
    .map((m) => ({ value: m.employeeId, label: `${m.employeeCode} - ${m.fullName}` }));
  return { options, loading: members.isLoading };
}
