import type { RequestStatus, RequestType } from '@/constants/requestTypes';
import type { Role } from '@/constants/roles';
import type { AuditEntry } from '@/types/attachment';

export type RequestTypeOption = {
  requestType: RequestType;
  label: string;
  needsApproval: boolean;
  handlerRole: string;
  /** Có = người tạo chọn vai trò xử lý trong danh sách này (đơn Khác). */
  handlerRoleOptions: Role[] | null;
};

export type RequestListItem = {
  requestId: number;
  requestCode: string;
  requestType: RequestType;
  status: RequestStatus;
  title: string | null;
  targetDate: string | null;
  createdByName: string | null;
  teamName: string | null;
  createdAt: string;
  decidedAt: string | null;
};

export type RequestLine = {
  requestLineId: number;
  materialId: number | null;
  materialName: string | null;
  materialColor: string | null;
  orderLineId: number | null;
  orderLineLabel: string | null;
  productComponentMaterialId: number | null;
  componentName: string | null;
  itemDescription: string | null;
  quantity: number;
  replenishQty: number | null;
};

export type RequestPermissions = {
  canEdit: boolean;
  canCancel: boolean;
  canApprove: boolean;
  canReject: boolean;
  canComplete: boolean;
  canNotComplete: boolean;
};

export type RequestDetail = {
  requestId: number;
  requestCode: string;
  requestType: RequestType;
  status: RequestStatus;
  title: string | null;
  content: string | null;
  targetDate: string | null;
  orderId: number | null;
  orderCode: string | null;
  /** Đơn xin NVL theo lệnh cắt (DEV-ALL-92). */
  workOrderId: number | null;
  workOrderCode: string | null;
  createdBy: number | null;
  createdByName: string | null;
  createdByTeamId: number | null;
  teamName: string | null;
  /** Đơn Khác: vai trò xử lý người tạo chọn; tổ trưởng thì kèm tổ. */
  handlerRole: Role | null;
  handlerTeamName: string | null;
  createdAt: string;
  decidedAt: string | null;
  decisionNote: string | null;
  resultNote: string | null;
  completedAt: string | null;
  shortageResolution: string | null;
  /** byte[] → base64 trong JSON; gửi nguyên chuỗi này khi sửa / duyệt / từ chối. */
  rowVer: string;
  lines: RequestLine[];
  history: AuditEntry[];
  permissions: RequestPermissions;
  maxPhotos: number;
};

export type RequestLineInput = {
  materialId?: number | null;
  orderLineId?: number | null;
  productComponentMaterialId?: number | null;
  itemDescription?: string | null;
  quantity: number;
};

export type CreateRequestBody = {
  requestType: RequestType;
  title?: string | null;
  content?: string | null;
  targetDate?: string | null;
  /** Đơn gắn với đơn hàng / phiếu hàng hỏng (vd. cắt bù từ phiếu Scrap). */
  orderId?: number | null;
  defectRecordId?: number | null;
  lines?: RequestLineInput[];
  /** Chỉ đơn Khác. */
  handlerRole?: Role | null;
  /** Đơn xin NVL theo lệnh cắt: tổng các đơn của lệnh không vượt trần (DEV-ALL-92). */
  workOrderId?: number | null;
};

export type UpdateRequestBody = Omit<CreateRequestBody, 'requestType'> & { rowVer: string };

export type RequestListQuery = {
  page: number;
  pageSize: number;
  keyword?: string;
  requestType?: RequestType;
  status?: RequestStatus | 'All';
};

export type RequestAction =
  | { kind: 'approve'; id: number; note?: string | null; rowVer: string }
  | { kind: 'reject'; id: number; reason: string; rowVer: string }
  | { kind: 'complete'; id: number; resultNote?: string | null; rowVer: string }
  | { kind: 'notComplete'; id: number; reason: string; rowVer: string }
  | { kind: 'cancel'; id: number; reason?: string | null; rowVer: string };
