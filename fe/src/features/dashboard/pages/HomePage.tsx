import { lazy, Suspense } from 'react';
import { ROLES } from '@/constants/roles';
import { useAuth } from '@/hooks/useAuth';
import { OrderProgressPanel } from '../components/OrderProgressPanel';

// Nạp lười để trang chủ không kéo cả các trang doanh thu vào gói chính.
const RevenuePanel = lazy(() =>
  import('@/features/revenue').then((m) => ({ default: m.RevenuePanel })),
);
const TeamKpiPanel = lazy(() =>
  import('@/features/kpi').then((m) => ({ default: m.TeamKpiPanel })),
);

export function HomePage() {
  const { user } = useAuth();
  if (!user) return null;
  const seesProgress = user.role === ROLES.Admin || user.role === ROLES.Manager;
  const seesTeamKpi =
    (user.role === ROLES.TeamLeader || user.role === ROLES.TeamDeputy) && user.teamType === 'Sewing';
  return (
    <>
      {user.role === ROLES.Admin && (
        <Suspense fallback={null}>
          <RevenuePanel />
        </Suspense>
      )}
      {seesProgress && <OrderProgressPanel />}
      {seesTeamKpi && (
        <Suspense fallback={null}>
          <TeamKpiPanel />
        </Suspense>
      )}
    </>
  );
}
