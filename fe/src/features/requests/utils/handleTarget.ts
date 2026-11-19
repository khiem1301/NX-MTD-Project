import { ROLES, type Role } from '@/constants/roles';
import { REQUEST_TYPE, type RequestType } from '@/constants/requestTypes';
import {
  ATTENDANCE_ROUTES,
  FROM_REQUEST_PARAM,
  FROM_REQUEST_TYPE_PARAM,
  ROUTES,
} from '@/constants/routes';

type HandleTarget = { path: string; roles: readonly Role[] };

/** Màn hình nghiệp vụ xử lý đơn đã duyệt, khớp người xử lý ở RequestTypePolicies (BE). Loại không có ở đây không có màn đích. */
const HANDLE_TARGET: Partial<Record<RequestType, HandleTarget>> = {
  [REQUEST_TYPE.MaterialRequisition]: {
    path: ROUTES.materialIssues,
    roles: [ROLES.WarehouseKeeper],
  },
  [REQUEST_TYPE.SemiFinishedRequisition]: {
    path: ROUTES.materialIssues,
    roles: [ROLES.WarehouseKeeper],
  },
  [REQUEST_TYPE.ReplacementCutting]: {
    path: ROUTES.workOrders,
    roles: [ROLES.Admin, ROLES.Manager],
  },
  [REQUEST_TYPE.AttendanceCorrection]: {
    path: ATTENDANCE_ROUTES.attendance,
    roles: [ROLES.Admin, ROLES.Manager, ROLES.Accountant],
  },
};

/** Đường dẫn tới màn xử lý đơn, hoặc null nếu loại đơn không có màn đích hay vai trò không được xử lý ở đó. */
export const getHandleUrl = (
  requestType: RequestType,
  requestId: number,
  role: Role | undefined,
): string | null => {
  const target = HANDLE_TARGET[requestType];
  if (!target || !role || !target.roles.includes(role)) return null;
  const query = new URLSearchParams({
    [FROM_REQUEST_PARAM]: String(requestId),
    [FROM_REQUEST_TYPE_PARAM]: requestType,
  });
  return `${target.path}?${query.toString()}`;
};
