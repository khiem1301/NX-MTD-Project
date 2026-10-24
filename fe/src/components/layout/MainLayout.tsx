import { Button, Layout, Menu, Typography } from 'antd';
import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { MENU_ENTRIES } from '@/app/menu';
import { MESSAGES } from '@/constants/messages';
import { useLogout } from '@/features/auth';
import { NotificationBell } from '@/features/notification';
import { useAuth } from '@/hooks/useAuth';

export function MainLayout() {
  const { user } = useAuth();
  const logout = useLogout();
  const location = useLocation();
  const navigate = useNavigate();

  const isTeamRole = user?.role === ROLES.TeamLeader || user?.role === ROLES.TeamDeputy;
  const items = MENU_ENTRIES.filter(
    (e) =>
      (e.roles === 'all' || (user !== null && e.roles.includes(user.role))) &&
      !(isTeamRole && e.teamTypes && !e.teamTypes.some((t) => t === user?.teamType)),
  ).map((e) => ({ key: e.path, label: e.label }));

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Layout.Sider breakpoint="lg" collapsedWidth={0}>
        <Typography.Title
          level={4}
          style={{ color: '#fff', textAlign: 'center', margin: '16px 0' }}
        >
          NX-MTD
        </Typography.Title>
        <Menu
          theme="dark"
          mode="inline"
          selectedKeys={[location.pathname]}
          items={items}
          onClick={({ key }) => navigate(key)}
        />
      </Layout.Sider>
      <Layout>
        <Layout.Header
          style={{
            display: 'flex',
            justifyContent: 'flex-end',
            alignItems: 'center',
            gap: 16,
            background: '#fff',
          }}
        >
          <NotificationBell />
          <Typography.Link onClick={() => navigate(ROUTES.profile)}>
            {user?.fullName} · {user?.roleName}
          </Typography.Link>
          <Button onClick={() => void logout()}>{MESSAGES.auth.logoutButton}</Button>
        </Layout.Header>
        <Layout.Content style={{ margin: 16 }}>
          <Outlet />
        </Layout.Content>
      </Layout>
    </Layout>
  );
}
