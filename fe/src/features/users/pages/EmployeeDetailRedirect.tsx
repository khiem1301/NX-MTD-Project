import { Navigate, useLocation, useParams } from 'react-router-dom';
import { ROUTES } from '@/constants/routes';

/** Link cũ /admin/employees/:id (hoặc /manager/employees/:id): chuyển sang danh sách và mở Modal chi tiết. */
export function EmployeeDetailRedirect() {
  const { id } = useParams();
  const { pathname } = useLocation();
  const base = pathname.slice(0, pathname.lastIndexOf('/'));
  return <Navigate to={ROUTES.employeeDetail(base, id ?? '')} replace />;
}
