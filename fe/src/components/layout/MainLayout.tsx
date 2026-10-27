import { useEffect, useMemo, useState } from 'react';
import { HomeOutlined } from '@ant-design/icons';
import { Drawer, Layout } from 'antd';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { HOME_ENTRY_KEY, MENU_GROUPS, SIDEBAR_HIDDEN } from '@/app/menuGroups';
import { MENU_ENTRIES } from '@/app/menu';
import { ROUTES } from '@/constants/routes';
import { useAuth } from '@/hooks/useAuth';
import { AppHeader } from './AppHeader';
import { buildNavigation, findActiveLeaf } from './menuModel';
import { SideMenu } from './SideMenu';
import { useMediaQuery } from './useMediaQuery';
import './layout.css';

const COLLAPSED_KEY = 'nxmtd.sider.collapsed';
const SIDER_WIDTH = 260;
const SIDER_COLLAPSED_WIDTH = 72;
const SIDER_BG = '#001529';

function readCollapsed(): boolean {
  try {
    return localStorage.getItem(COLLAPSED_KEY) === '1';
  } catch {
    return false;
  }
}

function writeCollapsed(value: boolean) {
  try {
    localStorage.setItem(COLLAPSED_KEY, value ? '1' : '0');
  } catch {
    // Trình duyệt chặn lưu trữ: chỉ mất việc nhớ trạng thái, không ảnh hưởng chức năng.
  }
}

function Brand({ collapsed, onClick }: { collapsed: boolean; onClick: () => void }) {
  return (
    <div
      className={collapsed ? 'app-brand app-brand--collapsed' : 'app-brand'}
      onClick={onClick}
      role="link"
      tabIndex={0}
      onKeyDown={(e) => e.key === 'Enter' && onClick()}
    >
      <div className="app-brand__logo">NX</div>
      {!collapsed && (
        <div>
          <div className="app-brand__name">NX-MTD</div>
          <div className="app-brand__tag">Quản lý xưởng may</div>
        </div>
      )}
    </div>
  );
}

export function MainLayout() {
  const { user } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const isDesktop = useMediaQuery('(min-width: 992px)');
  const showSearch = useMediaQuery('(min-width: 768px)');
  const [collapsed, setCollapsed] = useState(readCollapsed);
  const [drawerOpen, setDrawerOpen] = useState(false);

  const role = user?.role;
  const teamType = user?.teamType;
  const nav = useMemo(
    () =>
      buildNavigation(MENU_ENTRIES, MENU_GROUPS, role, SIDEBAR_HIDDEN, { key: HOME_ENTRY_KEY, icon: <HomeOutlined /> }, teamType),
    [role, teamType],
  );
  const active = findActiveLeaf(location.pathname, nav.leaves);

  useEffect(() => {
    document.title = active ? `${active.label} · NX-MTD` : 'NX-MTD';
  }, [active]);

  if (!user) return null;

  const go = (path: string) => {
    setDrawerOpen(false);
    navigate(path);
  };
  const toggleMenu = () => {
    if (!isDesktop) {
      setDrawerOpen(true);
      return;
    }
    const next = !collapsed;
    setCollapsed(next);
    writeCollapsed(next);
  };
  const menu = (inDrawer: boolean) => (
    <>
      <Brand collapsed={!inDrawer && collapsed} onClick={() => go(ROUTES.home)} />
      <div className="app-sider__scroll">
        <SideMenu
          items={nav.items}
          selectedPath={active?.path}
          activeGroupKey={active?.groupKey}
          collapsed={!inDrawer && collapsed}
          onNavigate={go}
        />
      </div>
    </>
  );

  return (
    <Layout style={{ minHeight: '100vh' }}>
      {isDesktop && (
        <Layout.Sider
          className="app-sider"
          width={SIDER_WIDTH}
          collapsedWidth={SIDER_COLLAPSED_WIDTH}
          collapsed={collapsed}
          trigger={null}
          collapsible
          theme="dark"
        >
          {menu(false)}
        </Layout.Sider>
      )}
      {!isDesktop && (
        <Drawer
          placement="left"
          width={SIDER_WIDTH + 20}
          open={drawerOpen}
          onClose={() => setDrawerOpen(false)}
          closable={false}
          styles={{ header: { display: 'none' }, body: { padding: 0, background: SIDER_BG, display: 'flex', flexDirection: 'column' } }}
        >
          {menu(true)}
        </Drawer>
      )}
      <Layout style={{ minWidth: 0 }}>
        <AppHeader
          user={user}
          leaves={nav.leaves}
          active={active}
          isDesktop={isDesktop}
          showSearch={showSearch}
          collapsed={collapsed}
          onToggleMenu={toggleMenu}
        />
        <Layout.Content className="app-content">
          <Outlet />
        </Layout.Content>
      </Layout>
    </Layout>
  );
}
