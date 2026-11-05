/** Mã (nhân viên, tổ, công đoạn, mã hàng, mã đơn): chữ, số và . _ - (khớp quy tắc ở BE). */
export const CODE_PATTERN = /^[A-Za-z0-9._-]+$/;

/** Dùng cho `normalize` của Form.Item: ô mã chỉ nhận chữ, số và . _ - (kể cả khi dán). */
export const normalizeCode = (value: string | undefined) => value?.replace(/[^A-Za-z0-9._-]/g, '');

/** Họ tên: chữ cái (có dấu tiếng Việt) và khoảng trắng giữa các từ; cho phép một dấu cách cuối khi đang gõ. Khớp PersonNameRule ở BE. */
export const PERSON_NAME_PATTERN = /^[\p{L}\p{M}]+(?: [\p{L}\p{M}]+)* ?$/u;

/** Dùng cho `normalize` của Form.Item: ô họ tên bỏ số và ký tự đặc biệt, gộp dấu cách thừa. */
export const normalizePersonName = (value: string | undefined) =>
  value
    ?.replace(/[^\p{L}\p{M} ]/gu, '')
    .replace(/ {2,}/g, ' ')
    .trimStart();
