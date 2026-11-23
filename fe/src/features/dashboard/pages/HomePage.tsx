import type { ReactElement } from 'react';
import { ROLES, type Role } from '@/constants/roles';
import { useAuth } from '@/hooks/useAuth';
import { AccountantHome } from '../components/home/AccountantHome';
import { AdminHome } from '../components/home/AdminHome';
import { ElectricianHome } from '../components/home/ElectricianHome';
import { ManagerHome } from '../components/home/ManagerHome';
import { QaqcHome } from '../components/home/QaqcHome';
import { TeamLeaderHome } from '../components/home/TeamLeaderHome';
import { WarehouseHome } from '../components/home/WarehouseHome';
import { WorkerHome } from '../components/home/WorkerHome';

/** Trang chủ của từng vai trò (DEV-ALL-168). */
const HOME_BY_ROLE: Record<Role, () => ReactElement> = {
  [ROLES.Admin]: () => <AdminHome />,
  [ROLES.Manager]: () => <ManagerHome />,
  [ROLES.Accountant]: () => <AccountantHome />,
  [ROLES.WarehouseKeeper]: () => <WarehouseHome />,
  [ROLES.TeamLeader]: () => <TeamLeaderHome />,
  [ROLES.TeamDeputy]: () => <TeamLeaderHome />,
  [ROLES.QaQc]: () => <QaqcHome />,
  [ROLES.Worker]: () => <WorkerHome />,
  [ROLES.Electrician]: () => <ElectricianHome />,
};

export function HomePage() {
  const { user } = useAuth();
  if (!user) return null;
  return HOME_BY_ROLE[user.role]();
}
