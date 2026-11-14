/** Giới hạn độ dài chữ dùng chung, khớp `TextLimits` ở BE (DEV-ALL-72). */
export const REASON_MAX_LENGTH = 500;
export const PASSWORD_MAX_LENGTH = 50;

/** Khổ vải / khổ bìa (m): lớn hơn 0, tối đa 3,5 m, 2 số lẻ; khớp `WidthLimits` ở BE (DEV-ALL-102). */
export const WIDTH_MIN_M = 0.01;
export const WIDTH_MAX_M = 3.5;
export const WIDTH_DECIMALS = 2;
