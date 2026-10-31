import { Card, Descriptions } from 'antd';
import { lazy, Suspense } from 'react';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { useAuth } from '@/hooks/useAuth';
import { LatestNotificationsCard } from '@/features/notification';
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
      <Card title={`${MESSAGES.home.greeting}, ${user.fullName}`}>
        <Descriptions column={1} size="small">
          <Descriptions.Item label="Vai trò">
            {user.roleName} ({user.role})
          </Descriptions.Item>
          <Descriptions.Item label="Tổ">{user.teamName ?? '—'}</Descriptions.Item>
          <Descriptions.Item label="Mã nhân viên">{user.username}</Descriptions.Item>
        </Descriptions>
      </Card>
      <LatestNotificationsCard />
      {user.role === ROLES.Admin && (
        <Suspense fallback={null}>
          <RevenuePanel />
        </Suspense>
      )}
      {seesProgress && <OrderProgressPanel />}
    </>
  );
}
