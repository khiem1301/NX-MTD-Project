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
