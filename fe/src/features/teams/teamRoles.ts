import type { TeamMember } from './types';

/** Vai trò được bổ nhiệm / thôi giữ chức trong tổ (D33). */
export const APPOINTABLE_ROLES = ['Worker', 'TeamLeader', 'TeamDeputy'];

export const isLeadership = (m: TeamMember) => m.role === 'TeamLeader' || m.role === 'TeamDeputy';
