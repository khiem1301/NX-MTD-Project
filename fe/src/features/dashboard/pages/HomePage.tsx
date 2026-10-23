import { Card, Descriptions } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { useAuth } from '@/hooks/useAuth';
import { OrderProgressPanel } from './OrderProgressPanel';

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
      {seesProgress && <OrderProgressPanel />}
    </>
  );
}
