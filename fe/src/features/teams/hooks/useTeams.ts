import { useQuery } from '@tanstack/react-query';
import { MESSAGES } from '@/constants/messages';
import { useApiMutation } from '@/hooks/useApiMutation';
import { useRolePath } from '@/hooks/useRolePath';
import { teamApi } from '../api/teamApi';
import type { AppointPosition, SaveTeamPayload, TransferPayload } from '../types';

export const TEAMS_KEY = ['teams'] as const;
/** Đổi tổ / bổ nhiệm làm đổi cả danh sách nhân viên, tra cứu thành viên và phiên của người bị ảnh hưởng. */
const invalidate = [TEAMS_KEY, ['employees'], ['hr-lookups']];

export const useTeams = (includeDiscontinued: boolean) => {
  const role = useRolePath();
  return useQuery({ queryKey: [...TEAMS_KEY, 'list', role, includeDiscontinued], queryFn: () => teamApi.list(role, includeDiscontinued) });
};

export const useTeam = (id: number | null) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...TEAMS_KEY, 'detail', role, id],
    queryFn: () => teamApi.get(role, id as number),
    enabled: id !== null,
  });
};

export const useSaveTeam = () => {
  const role = useRolePath();
  return useApiMutation(
    ({ id, body }: { id?: number; body: SaveTeamPayload }) =>
      id === undefined ? teamApi.create(role, body) : teamApi.update(role, id, body).then(() => id),
    { invalidate, successMessage: MESSAGES.common.saved },
  );
};

export const useTeamStatus = () => {
  const role = useRolePath();
  return useApiMutation(
    ({ id, active }: { id: number; active: boolean }) => (active ? teamApi.reactivate(role, id) : teamApi.discontinue(role, id)),
    { invalidate, successMessage: MESSAGES.common.saved },
  );
};

export const useTransferIn = () => {
  const role = useRolePath();
  return useApiMutation(({ id, body }: { id: number; body: TransferPayload }) => teamApi.transferIn(role, id, body), { invalidate });
};

export const useAppoint = () => {
  const role = useRolePath();
  return useApiMutation(
    ({ id, employeeId, position }: { id: number; employeeId: number; position: AppointPosition }) =>
      teamApi.appoint(role, id, employeeId, position),
    { invalidate },
  );
};
