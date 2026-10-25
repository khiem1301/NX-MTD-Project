import { MenuFoldOutlined, MenuUnfoldOutlined } from '@ant-design/icons';
import { Button, Layout } from 'antd';
import { NotificationBell } from '@/features/notification';
import type { UserInfo } from '@/types/auth';
import { GlobalSearch } from './GlobalSearch';
import type { NavLeaf } from './menuModel';
import { UserMenu } from './UserMenu';

type Props = {
  user: UserInfo;
  leaves: NavLeaf[];
  active: NavLeaf | undefined;
  isDesktop: boolean;
  showSearch: boolean;
  collapsed: boolean;
  onToggleMenu: () => void;
};

/** Thanh trên cùng: nút thu gọn menu, tên trang, tìm nhanh, thông báo và hồ sơ người dùng. */
export function AppHeader({ user, leaves, active, isDesktop, showSearch, collapsed, onToggleMenu }: Props) {
  const toggleIcon = isDesktop && !collapsed ? <MenuFoldOutlined /> : <MenuUnfoldOutlined />;
  return (
    <Layout.Header className="app-header">
      <Button
        type="text"
        icon={toggleIcon}
        onClick={onToggleMenu}
        aria-label="Mở hoặc thu gọn menu"
        style={{ fontSize: 18 }}
      />
      <div className="app-header__crumb">
        {active && active.groupLabel !== active.label && (
          <span className="app-header__crumb-group">{active.groupLabel}</span>
        )}
        <span className="app-header__crumb-title">{active?.label ?? 'NX-MTD'}</span>
      </div>
      <div className="app-header__spacer" />
      {showSearch && <GlobalSearch leaves={leaves} />}
      <NotificationBell />
      <UserMenu user={user} showName={showSearch} />
    </Layout.Header>
  );
}
