const NUMBER_INPUT_SELECTOR = '.ant-input-number-input';
const NOT_NUMERIC = /[^\d.,]/g;

const stripNotNumeric = (value: string) => value.replace(NOT_NUMERIC, '');

// Ghi qua setter gốc: React vẫn thấy giá trị đổi so với lần trước nên state không lệch với ô nhập.
const setNativeValue = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')?.set;

const sanitize = (event: Event) => {
  const input = event.target;
  if (!(input instanceof HTMLInputElement) || !input.matches(NUMBER_INPUT_SELECTOR)) return;
  if (event instanceof InputEvent && event.isComposing) return; // bộ gõ đang ghép chữ: lọc ở compositionend

  const { value, selectionStart } = input;
  const clean = stripNotNumeric(value);
  if (clean === value) return;
  const caret = stripNotNumeric(value.slice(0, selectionStart ?? value.length)).length;
  setNativeValue?.call(input, clean);
  input.setSelectionRange(caret, caret);
};

/**
 * Ô số của antd vẫn cho gõ chữ, rời ô mới trả lại giá trị cũ. Lọc một chỗ ở pha capture, trước khi React nhận sự kiện,
 * để mọi InputNumber chỉ nhận chữ số và dấu ngăn cách, kể cả khi dán. Hệ thống không có ô số âm nên chặn luôn dấu trừ.
 */
export const installNumericInputGuard = () => {
  document.addEventListener('input', sanitize, true);
  document.addEventListener('compositionend', sanitize, true);
};
