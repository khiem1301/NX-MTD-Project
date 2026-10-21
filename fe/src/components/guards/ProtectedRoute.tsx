import { Spin } from 'antd';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import type { Role } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';
import { useAuth } from '@/hooks/useAuth';

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
  return <Outlet />;
}
