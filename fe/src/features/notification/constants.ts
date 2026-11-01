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
