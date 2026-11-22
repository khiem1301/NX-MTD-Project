import type { StatusLabel } from '@/components/common/StatusTag';

export const VEHICLE_RECORD_STATUS_LABEL: Record<string, StatusLabel> = {
  ACTIVE: { text: 'Đang dùng', color: 'green' },
  DISCONTINUED: { text: 'Ngừng dùng', color: 'default' },
};

export const RUNNING_FILTER_OPTIONS = [
  { value: 'true', label: 'Đang chạy' },
  { value: 'false', label: 'Không chạy' },
];

export const VS_LABELS = {
  title: 'Xe và trạng thái hoạt động',
  plate: 'Biển số',
  vehicleType: 'Loại xe',
  recordStatus: 'Tình trạng xe',
  running: 'Đang chạy',
  idle: 'Rảnh',
  runningFilter: 'Trạng thái chạy',
  driver: 'Tài xế',
  dispatch: 'Phiếu xuất',
  tripDate: 'Ngày chuyến',
  odometerStart: 'Km đầu chuyến',
  lastOdometer: 'Km hiện tại',
  openTripWarning: 'chuyến chưa kết thúc',
} as const;

export const VS_MESSAGES = {
  multiOpenTrips: 'Xe có nhiều hơn một chuyến chưa kết thúc, cần Kế toán kiểm tra sổ xe.',
} as const;
