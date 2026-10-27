import { Spin } from 'antd';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { MENU_ENTRIES } from '@/app/menu';
import { matchesTeamType, type Role } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';
import { useAuth } from '@/hooks/useAuth';

/** Loại tổ được vào trang này, lấy từ mục menu cùng đường dẫn (khớp dài nhất) để menu và route không lệch nhau. */
function teamTypesOf(pathname: string) {
  let best: (typeof MENU_ENTRIES)[number] | undefined;
  for (const entry of MENU_ENTRIES) {
    if (entry.path === '/') continue;
    if (pathname !== entry.path && !pathname.startsWith(`${entry.path}/`)) continue;
    if (!best || entry.path.length > best.path.length) best = entry;
  }
  return best?.teamTypes;
}

export function ProtectedRoute({ roles }: { roles?: Role[] }) {
  const { status, user } = useAuth();
  const location = useLocation();

  if (status === 'loading') {
    return (
      <div style={{ minHeight: '100vh', display: 'grid', placeItems: 'center' }}>
        <Spin />
      </div>
    );
  }
  if (status !== 'authenticated' || !user) {
    return <Navigate to={ROUTES.login} replace state={{ from: location }} />;
  }
  // D29: mật khẩu tạm (tạo mới / đặt lại) → phải đổi mật khẩu trước khi dùng tiếp.
  if (user.isTemporaryPassword && location.pathname !== ROUTES.profile) {
    return <Navigate to={`${ROUTES.profile}?changePassword=1`} replace />;
  }
  if (roles && !roles.includes(user.role)) {
    return <Navigate to={ROUTES.home} replace />;
  }
  // Tổ cắt và tổ may cùng vai trò Tổ trưởng / Công nhân nhưng khác chức năng.
  if (!matchesTeamType(user.role, user.teamType, teamTypesOf(location.pathname))) {
    return <Navigate to={ROUTES.home} replace />;
  }
  return <Outlet />;
}
