import type { Role } from '@/constants/roles';

export type TeamType = 'Cutting' | 'Sewing' | 'Office';

export type UserInfo = {
  accountId: number;
  employeeId: number;
  username: string;
  fullName: string;
  role: Role;
  roleName: string;
  teamId: number | null;
  teamName: string | null;
  teamType: TeamType | null;
  isTemporaryPassword: boolean;
};

export type AuthTokens = {
  accessToken: string;
  accessExpiresAt: string;
  refreshToken: string;
  refreshExpiresAt: string;
};

export type LoginResult = AuthTokens & { user: UserInfo };
export type LoginRequest = { username: string; password: string };
