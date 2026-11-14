// Hằng số của màn Thông báo.

export const SCOPE_LABEL: Record<string, string> = {
  ALL: 'Toàn xưởng',
  TEAM: 'Theo tổ',
  ROLE: 'Theo vai trò',
};

export const SCOPE_OPTIONS = Object.entries(SCOPE_LABEL).map(([value, label]) => ({
  value,
  label,
}));

export const PRIORITY_OPTIONS = [
  { value: 'NORMAL', label: 'Thường' },
  { value: 'URGENT', label: 'Khẩn' },
];

/** Notification.RefTable của thông báo mở cửa sổ chi tiết tại chỗ (khớp NotificationRefs ở BE). */
export const NOTIFICATION_REF = {
  /** Lệch kiểm cuối đơn của tổ: refId = id chỉ tiêu KPI (DEV-ALL-107). */
  KPI_MISMATCH: 'KpiMismatch',
} as const;
