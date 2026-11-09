import type { StatusLabel } from '@/components/common/StatusTag';

export const VEHICLE_STATUS_LABEL: Record<string, StatusLabel> = {
  ACTIVE: { text: 'Đang dùng', color: 'green' },
  DISCONTINUED: { text: 'Ngừng dùng', color: 'default' },
};

export const TRIP_STATUS = { STARTED: 'STARTED', COMPLETED: 'COMPLETED' } as const;

export const TRIP_STATUS_LABEL: Record<string, StatusLabel> = {
  STARTED: { text: 'Đang chạy', color: 'blue' },
  COMPLETED: { text: 'Đã kết thúc', color: 'green' },
};

export const FUEL_PHOTO_MAX = 5;

export const VL_LABELS = {
  title: 'Sổ xe và nhiên liệu',
  tripsTab: 'Chuyến xe',
  vehiclesTab: 'Danh mục xe',
  addVehicle: 'Thêm xe',
  editVehicle: 'Sửa xe',
  startTrip: 'Ghi chuyến mới',
  completeTrip: 'Kết thúc chuyến',
  plate: 'Biển số',
  vehicleType: 'Loại xe',
  fuelNorm: 'Định mức (lít / 100 km)',
  status: 'Trạng thái',
  tripCount: 'Số chuyến',
  lastOdometer: 'Km hiện tại',
  discontinue: 'Ngừng dùng',
  reactivate: 'Dùng lại',
  date: 'Ngày',
  driver: 'Tài xế',
  purpose: 'Mục đích',
  dispatch: 'Phiếu xuất',
  odometerStart: 'Km đầu',
  odometerEnd: 'Km cuối',
  distance: 'Quãng đường (km)',
  fuelLiters: 'Xăng (lít)',
  fuelNormLiters: 'Định mức (lít)',
  fuelCost: 'Tiền xăng',
  warnings: 'Cảnh báo',
  kmGap: 'Lệch km',
  fuelOver: 'Xăng vượt định mức',
  warningsOnly: 'Chỉ chuyến có cảnh báo',
  vehicle: 'Xe',
  selectVehicle: 'Chọn xe',
  receiptPhotos: 'Ảnh phiếu xăng',
  photos: 'Ảnh',
} as const;

export const VL_MESSAGES = {
  plateInvalid: 'Biển số không đúng định dạng (ví dụ 51C-123.45 hoặc 30F-1234).',
  fuelBothRequired: 'Phải nhập cả số xăng và tiền xăng, hoặc để trống cả hai.',
  vehicleSaved: 'Đã lưu xe.',
  tripStarted: 'Đã ghi chuyến, chờ kết thúc chuyến.',
  tripCompleted: 'Đã kết thúc chuyến.',
  confirmDiscontinue:
    'Ngừng sử dụng xe này? Xe không được ghi thêm chuyến nhưng dữ liệu cũ vẫn giữ.',
} as const;

/** Biển số ô tô: mã tỉnh, 1-2 chữ cái, gạch ngang, 4-5 số (biển mới có dấu chấm trước 2 số cuối); khớp VehiclePlate ở BE. */
export const PLATE_PATTERN = /^[1-9]\d[A-Z]{1,2}\d?-(\d{3}\.\d{2}|\d{4,5})$/;
export const PLATE_EXAMPLE = '51C-123.45';

/** Trần nhập liệu của xe / chuyến xe, khớp VehicleLimits ở BE. */
export const VEHICLE_LIMITS = {
  maxOdometer: 9_999_999,
  maxFuelLiters: 1_000,
  maxFuelCost: 100_000_000,
  maxFuelNorm: 9_999,
} as const;
