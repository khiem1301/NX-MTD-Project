import { Navigate, useSearchParams } from 'react-router-dom';
import { ORDER_MATERIALS_TAB } from '@/constants/orderStatus';
import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';
import { usePermission } from '@/hooks/usePermission';

/** Thông báo "Có đơn hàng mới" đã gửi trước đây trỏ đường dẫn cũ: chuyển tới tab NVL của đơn, không có đơn thì tới danh sách đơn. */
export function OrderMaterialLinkRedirect() {
  const [params] = useSearchParams();
  const { hasRole } = usePermission();
  const isAdmin = hasRole(ROLES.Admin);
  const orderId = Number(params.get('orderId'));

  if (!orderId) return <Navigate to={isAdmin ? ROUTES.orders : ROUTES.managerOrders} replace />;
  const detail = isAdmin ? ROUTES.orderDetail(orderId) : ROUTES.managerOrderDetail(orderId);
  return <Navigate to={`${detail}?tab=${ORDER_MATERIALS_TAB}`} replace />;
}
