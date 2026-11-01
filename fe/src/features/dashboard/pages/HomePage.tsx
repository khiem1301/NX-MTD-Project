import { lazy, Suspense } from 'react';
import { ROLES } from '@/constants/roles';
import { useAuth } from '@/hooks/useAuth';
import { OrderProgressPanel } from '../components/OrderProgressPanel';

// Nạp lười để trang chủ không kéo cả các trang doanh thu vào gói chính.
const RevenuePanel = lazy(() =>
  import('@/features/revenue').then((m) => ({ default: m.RevenuePanel })),
);

export function HomePage() {
  const { user } = useAuth();
  if (!user) return null;
  const seesProgress = user.role === ROLES.Admin || user.role === ROLES.Manager;
  return (
    <>
      {user.role === ROLES.Admin && (
        <Suspense fallback={null}>
          <RevenuePanel />
        </Suspense>
      )}
      {seesProgress && <OrderProgressPanel />}
    </>
  );
}
