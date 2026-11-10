import { Suspense } from 'react';
import { RevenuePanelSlot, TeamKpiPanelSlot } from '@/app/featureSlots';
import { ROLES } from '@/constants/roles';
import { useAuth } from '@/hooks/useAuth';
import { OrderProgressPanel } from '../components/OrderProgressPanel';

export function HomePage() {
  const { user } = useAuth();
  if (!user) return null;
  const seesProgress = user.role === ROLES.Admin || user.role === ROLES.Manager;
  const seesTeamKpi =
    (user.role === ROLES.TeamLeader || user.role === ROLES.TeamDeputy) &&
    user.teamType === 'Sewing';
  return (
    <>
      {user.role === ROLES.Admin && (
        <Suspense fallback={null}>
          <RevenuePanelSlot />
        </Suspense>
      )}
      {seesProgress && <OrderProgressPanel />}
      {seesTeamKpi && (
        <Suspense fallback={null}>
          <TeamKpiPanelSlot />
        </Suspense>
      )}
    </>
  );
}
