import type { StatusLabel } from '@/components/common/StatusTag';
import type { DeliveryMethod } from './types';

export const DISPATCH_STATUS = {
  PENDING: 'PENDING',
  REJECTED: 'REJECTED',
  CANCELLED: 'CANCELLED',
  IN_DELIVERY: 'IN_DELIVERY',
  DELIVERED: 'DELIVERED',
} as const;

export const DISPATCH_STATUS_LABEL: Record<string, StatusLabel> = {
  PENDING: { text: 'Chờ xuất kho', color: 'gold' },
  REJECTED: { text: 'Kho từ chối', color: 'red' },
  CANCELLED: { text: 'Đã hủy', color: 'default' },
  IN_DELIVERY: { text: 'Đang giao', color: 'blue' },
  DELIVERED: { text: 'Đã giao', color: 'green' },
};

export const DISPATCH_PHOTO_MAX = 5;

/** Khớp DispatchLimits ở BE: số lượng mỗi dòng tối đa 1.000.000, ngày giao trong 366 ngày tới. */
export const DISPATCH_MAX_QTY = 1_000_000;
export const DISPATCH_MAX_DAYS_AHEAD = 366;

export const DISPATCH_LABELS = {
  title: 'Phiếu xuất hàng',
  create: 'Lập phiếu xuất',
  code: 'Mã phiếu',
  order: 'Đơn hàng',
  customer: 'Khách hàng',
  deliveryDate: 'Ngày giao',
  deliveryAddress: 'Địa chỉ giao hàng',
  note: 'Ghi chú',
  status: 'Trạng thái',
  createdAt: 'Ngày lập',
  issueQty: 'Số xuất',
  orderLine: 'Mã hàng - màu - size',
  orderedQty: 'Đặt',
  issuedQty: 'Đã xuất',
  remainingQty: 'Còn xuất được',
  availableQty: 'Tồn khả dụng',
  exportQty: 'Xuất lần này',
  selectOrder: 'Chọn đơn hàng',
  dispatchedBy: 'Thủ kho xuất',
  customerReceivedDate: 'Ngày khách nhận',
  requiredPhoto: 'bắt buộc',
  needIssuePhoto: 'Cần ảnh phiếu xuất có Quản đốc ký trước khi xuất hàng.',
  needDeliveryPhoto: 'Cần ảnh biên bản giao có khách ký trước khi xác nhận khách nhận.',
  export: 'Xác nhận xuất hàng',
  reject: 'Từ chối',
  rejectTitle: 'Từ chối phiếu xuất hàng',
  rejectReason: 'Lý do từ chối',
  cancel: 'Hủy phiếu',
  confirmDelivery: 'Xác nhận khách đã nhận',
  issueSlipPhotos: 'Ảnh phiếu xuất có Quản đốc ký',
  deliveryNotePhotos: 'Ảnh biên bản giao có khách ký',
  // DEV-ALL-127: Thủ kho chọn cách giao khi xuất kho.
  exportTitle: 'Xuất hàng · {0}',
  exportHint: 'Tồn kho thành phẩm giảm theo số trên phiếu. Chọn cách giao hàng.',
  deliveryMethod: 'Cách giao',
  vehicle: 'Xe giao hàng',
  vehiclePlaceholder: 'Chọn xe đang rảnh',
  noVehicle: 'Không có xe nào đang rảnh (xe ngừng dùng hoặc còn chuyến chưa kết thúc).',
  driver: 'Tài xế',
  odometerStart: 'Km lúc đi',
  odometerHint: 'Km cuối chuyến trước: {0}',
  tripHint: 'Hệ thống tạo chuyến xe đang chạy; Kế toán ghi km về và xăng ở Sổ xe.',
  vehicleRequired: 'Vui lòng chọn xe giao hàng.',
  driverRequired: 'Vui lòng nhập tên tài xế.',
  odometerRequired: 'Nhập km lúc đi (từ 0 trở lên).',
} as const;

export const DELIVERY_METHOD = {
  CUSTOMER_PICKUP: 'CUSTOMER_PICKUP',
  COMPANY_VEHICLE: 'COMPANY_VEHICLE',
} as const;

export const DELIVERY_METHOD_LABEL: Record<DeliveryMethod, string> = {
  CUSTOMER_PICKUP: 'Khách tự lấy hàng',
  COMPANY_VEHICLE: 'Xe công ty giao',
};

/** Khớp VehicleLimits.MaxOdometer / độ dài tên tài xế ở BE. */
export const MAX_ODOMETER = 9_999_999;
export const DRIVER_NAME_MAX = 100;

export const DISPATCH_MESSAGES = {
  created: 'Đã lập phiếu xuất hàng, thành phẩm đã được giữ chỗ.',
  confirmCreate:
    'Lập phiếu xuất hàng? Số xuất sẽ được giữ chỗ trong kho thành phẩm đến khi kho xuất hoặc từ chối.',
  confirmCancel: 'Hủy phiếu xuất hàng này? Số giữ chỗ sẽ được nhả.',
  cancelled: 'Đã hủy phiếu xuất hàng.',
  exported: 'Đã xuất hàng.',
  rejected: 'Đã từ chối phiếu xuất hàng.',
  delivered: 'Đã xác nhận khách nhận hàng.',
  addressRequired: 'Vui lòng nhập địa chỉ giao hàng.',
} as const;
