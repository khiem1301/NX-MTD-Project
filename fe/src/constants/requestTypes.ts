export const REQUEST_TYPE = {
  SpareParts: 'SpareParts',
  NewMachine: 'NewMachine',
  MachineSupplies: 'MachineSupplies',
  AttendanceCorrection: 'AttendanceCorrection',
  MaterialSupplement: 'MaterialSupplement',
  DefectiveMaterial: 'DefectiveMaterial',
  ReplacementCutting: 'ReplacementCutting',
  ShortDelivery: 'ShortDelivery',
  MaterialRequisition: 'MaterialRequisition',
  SemiFinishedRequisition: 'SemiFinishedRequisition',
  Overtime: 'Overtime',
} as const;
export type RequestType = (typeof REQUEST_TYPE)[keyof typeof REQUEST_TYPE];

/** Khớp RequestTypePolicies.Label ở BE. */
export const REQUEST_TYPE_LABEL: Record<RequestType, string> = {
  SpareParts: 'Linh kiện',
  NewMachine: 'Máy mới',
  MachineSupplies: 'Vật tư máy',
  AttendanceCorrection: 'Chấm công bù',
  MaterialSupplement: 'Bổ sung NVL',
  DefectiveMaterial: 'NVL lỗi',
  ReplacementCutting: 'Cắt bù',
  ShortDelivery: 'Thiếu hàng khi nhập',
  MaterialRequisition: 'Xin NVL',
  SemiFinishedRequisition: 'Xin BTP',
  Overtime: 'Tăng ca',
};

export const REQUEST_STATUS = {
  PendingApproval: 'PendingApproval',
  PendingProcessing: 'PendingProcessing',
  Approved: 'Approved',
  Rejected: 'Rejected',
  Completed: 'Completed',
  Cancelled: 'Cancelled',
} as const;
export type RequestStatus = (typeof REQUEST_STATUS)[keyof typeof REQUEST_STATUS];

export const REQUEST_STATUS_LABEL: Record<RequestStatus, string> = {
  PendingApproval: 'Chờ duyệt',
  PendingProcessing: 'Chờ xử lý',
  Approved: 'Đã duyệt',
  Rejected: 'Từ chối',
  Completed: 'Hoàn thành',
  Cancelled: 'Đã hủy',
};
