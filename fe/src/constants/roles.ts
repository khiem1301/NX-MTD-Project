export const ROLES = {
  Admin: 'Admin',
  Manager: 'Manager',
  Accountant: 'Accountant',
  WarehouseKeeper: 'WarehouseKeeper',
  TeamLeader: 'TeamLeader',
  TeamDeputy: 'TeamDeputy',
  QaQc: 'QaQc',
  Electrician: 'Electrician',
  Worker: 'Worker',
} as const;

export type Role = (typeof ROLES)[keyof typeof ROLES];

/** Vai trò gắn với một tổ: tổ cắt và tổ may cùng vai trò nhưng khác chức năng. */
export const TEAM_SCOPED_ROLES: readonly Role[] = [ROLES.TeamLeader, ROLES.TeamDeputy, ROLES.Worker];

/**
 * Người dùng có vào được chức năng giới hạn theo loại tổ không.
 * Vai trò không thuộc tổ (Quản đốc, Thủ kho...) không bị lọc.
 */
export function matchesTeamType(
  role: Role | undefined,
  teamType: 'Cutting' | 'Sewing' | 'Office' | null | undefined,
  allowed: readonly ('Cutting' | 'Sewing' | 'Office')[] | undefined,
): boolean {
  if (!allowed || role === undefined || !TEAM_SCOPED_ROLES.includes(role)) return true;
  return teamType !== null && teamType !== undefined && allowed.includes(teamType);
}
