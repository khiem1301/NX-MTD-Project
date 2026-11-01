/** Số điện thoại hợp lệ: 10 số bắt đầu bằng 0 (vd. 0901234567) hoặc +84 kèm 9 số. Khớp quy tắc ở BE (PhoneRule). */
export const PHONE_PATTERN = /^(?:0\d{9}|\+84\d{9})$/;

/** Dùng cho `normalize` của Form.Item: ô số điện thoại chỉ nhận chữ số và dấu +. */
export const normalizePhone = (value: string | undefined) => value?.replace(/[^\d+]/g, '');

export const PHONE_INVALID_MESSAGE =
  'Số điện thoại không hợp lệ (10 số bắt đầu bằng 0, hoặc +84 và 9 số).';

/** Rule antd cho ô số điện thoại; ô trống không bị rule này báo lỗi (bắt buộc hay không do rule `required` riêng). */
export const phoneRule = { pattern: PHONE_PATTERN, message: PHONE_INVALID_MESSAGE };
