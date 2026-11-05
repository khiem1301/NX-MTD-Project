import { InputNumber, type InputNumberProps } from 'antd';
import { MAX_PIECE_QTY } from '@/constants/quantity';
import { formatQtyTyping, formatQtyValue, parseQtyInput } from '@/utils/qtyInput';

export type QtyInputProps = Omit<
  InputNumberProps<number>,
  'formatter' | 'parser' | 'precision' | 'min' | 'max' | 'stringMode'
> & {
  /** Mặc định 0. */
  min?: number;
  /** Mặc định MAX_PIECE_QTY (1.000.000). Gõ quá thì ô tự thành số này. */
  max?: number;
  /** Số chữ số lẻ: 0 cho hàng chiếc (mặc định), MATERIAL_QTY_DECIMALS cho NVL. */
  decimals?: number;
};

// Ghi qua setter gốc rồi phát sự kiện input: antd đọc lại chữ trong ô như khi người dùng gõ.
const setNativeValue = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')?.set;

/**
 * Chữ sắp chèn (phím vừa gõ / chữ vừa dán) làm số vượt `max` thì chặn và ghi luôn `max` vào ô.
 * Cần làm ở đây vì antd chỉ định dạng lại khi giá trị đổi: ô đã ở `max` mà gõ thêm số thì chữ vẫn dài ra.
 */
const clampInsert = (
  event: React.KeyboardEvent<HTMLInputElement> | React.ClipboardEvent<HTMLInputElement>,
  inserted: string,
  decimals: number,
  max: number,
) => {
  const input = event.target;
  if (!(input instanceof HTMLInputElement)) return;
  const { value, selectionStart, selectionEnd } = input;
  const next =
    value.slice(0, selectionStart ?? value.length) +
    inserted +
    value.slice(selectionEnd ?? value.length);
  if (Number(parseQtyInput(next, decimals, Infinity)) <= max) return;
  event.preventDefault();
  setNativeValue?.call(input, formatQtyValue(max, decimals));
  input.dispatchEvent(new Event('input', { bubbles: true }));
};

/**
 * Ô nhập số lượng dùng chung: dấu chấm ngăn nghìn hiện ngay khi gõ (1.250.000), phần lẻ sau dấu phẩy (12,5).
 * Gõ hoặc dán số vượt `max` thì ô tự thành `max`; nhỏ hơn `min` thì antd sửa về `min` khi rời ô.
 * Phải chuyển tiếp props vì Form.Item truyền value / onChange vào phần tử con trực tiếp.
 */
export function QtyInput({
  min = 0,
  max = MAX_PIECE_QTY,
  decimals = 0,
  onKeyDown,
  onPaste,
  ...props
}: QtyInputProps) {
  return (
    <InputNumber<number>
      {...props}
      min={min}
      max={max}
      precision={decimals}
      formatter={(value, { userTyping, input }) =>
        userTyping ? formatQtyTyping(input, decimals, max) : formatQtyValue(value, decimals)
      }
      // antd nhận chuỗi số chuẩn ("1234.5"); trả chuỗi để ô trống là rỗng chứ không thành 0.
      parser={(text) => parseQtyInput(text, decimals, max) as unknown as number}
      onKeyDown={(e) => {
        // Dấu chấm do ô tự thêm; ô số nguyên không có phần lẻ nên chặn cả dấu phẩy.
        if (e.key === '.' || (decimals === 0 && e.key === ',')) e.preventDefault();
        else if (/^\d$/.test(e.key)) clampInsert(e, e.key, decimals, max);
        onKeyDown?.(e);
      }}
      onPaste={(e) => {
        clampInsert(e, e.clipboardData.getData('text'), decimals, max);
        onPaste?.(e);
      }}
    />
  );
}
