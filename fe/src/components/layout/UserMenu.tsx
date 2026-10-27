import { useState } from 'react';
import { DownOutlined, KeyOutlined, LogoutOutlined, UserOutlined } from '@ant-design/icons';
import { Dropdown, Menu } from 'antd';
import { useNavigate } from 'react-router-dom';
import { MESSAGES } from '@/constants/messages';
import { ROUTES } from '@/constants/routes';
import { useLogout } from '@/features/auth';
import type { UserInfo } from '@/types/auth';
import { UserAvatar } from './UserAvatar';

const CHANGE_PASSWORD_PATH = `${ROUTES.profile}?changePassword=1`;

/** Hồ sơ người dùng ở góc trên phải: xem thông tin, vào hồ sơ, đổi mật khẩu và đăng xuất. */
export function UserMenu({ user, showName }: { user: UserInfo; showName: boolean }) {
  const [open, setOpen] = useState(false);
  const navigate = useNavigate();
  const logout = useLogout();

  const popup = (
    <div className="app-user-pop">
      <div className="app-user-pop__head">
        <UserAvatar name={user.fullName} size={44} />
        <div style={{ minWidth: 0 }}>
          <div className="app-user-pop__name">{user.fullName}</div>
          <div className="app-user-pop__meta">
            {user.roleName}
            {user.teamName ? ` · ${user.teamName}` : ''}
          </div>
          <div className="app-user-pop__meta">Mã NV: {user.username}</div>
        </div>
      </div>
      <Menu
        selectable={false}
        items={[
          { key: 'profile', icon: <UserOutlined />, label: 'Hồ sơ của tôi' },
          { key: 'password', icon: <KeyOutlined />, label: 'Đổi mật khẩu' },
          { type: 'divider' },
          { key: 'logout', icon: <LogoutOutlined />, label: MESSAGES.auth.logoutButton, danger: true },
        ]}
        onClick={({ key }) => {
          setOpen(false);
          if (key === 'profile') navigate(ROUTES.profile);
          else if (key === 'password') navigate(CHANGE_PASSWORD_PATH);
          else void logout();
        }}
      />
    </div>
  );

  return (
    <Dropdown open={open} onOpenChange={setOpen} trigger={['click']} placement="bottomRight" popupRender={() => popup}>
      <button type="button" className="app-user" aria-label={user.fullName}>
        <UserAvatar name={user.fullName} />
        {showName && (
          <span className="app-user__text">
            <span className="app-user__name">{user.fullName}</span>
            <span className="app-user__role">{user.roleName}</span>
          </span>
        )}
        {showName && <DownOutlined style={{ fontSize: 10, color: '#8c8c8c' }} />}
      </button>
    </Dropdown>
  );
}
