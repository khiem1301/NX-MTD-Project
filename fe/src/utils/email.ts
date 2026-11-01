/**
 * Email hợp lệ: dạng ten@mien.com, chỉ ký tự ASCII (cột Email ở database là VARCHAR nên chữ có dấu sẽ bị đổi thành "?"),
 * tên miền có ít nhất một dấu chấm, không có khoảng trắng. Khớp quy tắc ở BE (EmailRule).
 */
export const EMAIL_PATTERN =
  /^[A-Za-z0-9.!#$%&'*+/=?^_`{|}~-]+@[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?)+$/;

export const EMAIL_INVALID_MESSAGE =
  'Email không hợp lệ (dạng ten@mien.com, chỉ dùng chữ không dấu, số và ký tự thông thường).';
