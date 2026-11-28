import type { PageParams } from '@/types/api';

export type DispatchNoteListItem = {
  dispatchNoteId: number;
  dispatchCode: string;
  orderId: number;
  orderCode: string;
  customerName: string;
  deliveryDate: string;
  status: string;
  createdAt: string;
  totalIssueQty: number;
};

export type DispatchNoteLine = {
  dispatchNoteLineId: number;
  orderLineId: number;
  productCode: string;
  color: string;
  sizeCode: string;
  issueQty: number;
};

export type DispatchNote = Omit<DispatchNoteListItem, 'totalIssueQty'> & {
  deliveryAddress: string;
  note: string | null;
  rejectReason: string | null;
  dispatchedByName: string | null;
  dispatchedAt: string | null;
  customerReceivedDate: string | null;
  lines: DispatchNoteLine[];
  /** Phiên bản phiếu đang xem; gửi lại khi hủy, xuất, từ chối, xác nhận giao để phát hiện người khác đã đổi phiếu. */
  rowVer: string;
  /** DEV-ALL-127: cách giao Thủ kho chọn khi xuất; xe công ty thì kèm biển số và tài xế của chuyến. */
  deliveryMethod: DeliveryMethod | null;
  vehiclePlate: string | null;
  driverName: string | null;
};

export type DeliveryMethod = 'CUSTOMER_PICKUP' | 'COMPANY_VEHICLE';

export type ExportDispatchPayload = {
  rowVer: string;
  deliveryMethod: DeliveryMethod;
  vehicleId: number | null;
  driverName: string | null;
  odometerStart: number | null;
};

/** Xe đang hoạt động và chưa có chuyến đang chạy; lastOdometer hiện thành gợi ý dưới ô km lúc đi. */
export type AvailableVehicle = {
  vehicleId: number;
  plateNumber: string;
  vehicleType: string;
  lastOdometer: number | null;
};

export type DispatchableLine = {
  orderLineId: number;
  productCode: string;
  color: string;
  sizeCode: string;
  orderedQty: number;
  issuedQty: number;
  remainingQty: number;
  availableQty: number;
};

export type DispatchNoteQuery = PageParams & { status?: string; orderId?: number };

export type CreateDispatchPayload = {
  orderId: number;
  deliveryDate: string;
  deliveryAddress: string;
  note?: string;
  lines: { orderLineId: number; issueQty: number }[];
};

export type ConfirmDeliveryPayload = {
  customerReceivedDate: string;
  rowVer: string;
};
