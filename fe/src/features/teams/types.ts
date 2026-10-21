import type { PayType, TeamType } from '@/constants/hr';
import type { Role } from '@/constants/roles';
import type { AuditEntry } from '@/types/attachment';

export type TeamStatus = 'Active' | 'Discontinued';

export type TeamListItem = {
  teamId: number;
  teamCode: string;
  teamName: string;
  teamType: TeamType;
  status: TeamStatus;
  leaderName: string | null;
  deputyName: string | null;
  memberCount: number;
};

export type TeamMember = {
  employeeId: number;
  employeeCode: string;
  fullName: string;
  role: Role;
  roleName: string;
  payType: PayType;
  currentGradeCode: string | null;
};

export type TeamDetail = {
  teamId: number;
  teamCode: string;
  teamName: string;
  teamType: TeamType;
  status: TeamStatus;
  members: TeamMember[];
  history: AuditEntry[];
};

export type SaveTeamPayload = { teamCode: string; teamName: string; teamType: TeamType };
export type TransferPayload = { employeeIds: number[]; effectiveDate: string; reason?: string | null };
export type AppointPosition = 'Leader' | 'Deputy';
