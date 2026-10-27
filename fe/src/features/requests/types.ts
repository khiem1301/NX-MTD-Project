import type { RequestStatus, RequestType } from '@/constants/requestTypes';
import type { AuditEntry } from '@/types/attachment';

export type RequestTypeOption = {
  requestType: RequestType;
  label: string;
  needsApproval: boolean;
  handlerRole: string;
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
  createdBy: number | null;
  createdByName: string | null;
  createdByTeamId: number | null;
  teamName: string | null;
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
  materialColor?: string | null;
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
};

export type UpdateRequestBody = Omit<CreateRequestBody, 'requestType'> & { rowVer: string };

export type RequestListQuery = {
  page: number;
  pageSize: number;
  keyword?: string;
  requestType?: RequestType;
  status?: RequestStatus;
};

export type RequestAction =
  | { kind: 'approve'; id: number; note?: string | null; rowVer: string }
  | { kind: 'reject'; id: number; reason: string; rowVer: string }
  | { kind: 'complete'; id: number; resultNote?: string | null; rowVer: string }
  | { kind: 'cancel'; id: number; reason?: string | null; rowVer: string };
