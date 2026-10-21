import { useQuery } from '@tanstack/react-query';
import { hrLookupApi } from '@/services/hrLookupApi';

const KEY = ['hr-lookups'] as const;

export const useTeamLookup = () => useQuery({ queryKey: [...KEY, 'teams'], queryFn: hrLookupApi.teams });

export const useTeamMemberLookup = (teamId: number | null) =>
  useQuery({
    queryKey: [...KEY, 'team-members', teamId],
    queryFn: () => hrLookupApi.teamMembers(teamId as number),
    enabled: teamId !== null,
  });
