import { InputNumber, Typography, type InputNumberProps } from 'antd';
import { MAX_PIECE_QTY } from '@/constants/quantity';
import { formatQtyTyping, formatQtyValue, maxIntDigits, parseQtyInput } from '@/utils/qtyInput';
import { exceedsLimit, overLimitText } from '@/utils/qtyLimit';

export type QtyInputProps = Omit<
  InputNumberProps<number>,
  'formatter' | 'parser' | 'precision' | 'min' | 'max' | 'stringMode'
> & {
  /** Mặc định 0. */
  min?: number;
  /**
   * Trần cố định, mặc định MAX_PIECE_QTY (1.000.000). Được gõ vượt (hiện cảnh báo vàng) nhưng không được nhiều
   * chữ số hơn trần: trần 1.000.000 cho gõ 2.000.000 chứ không gõ thêm chữ số.
   */
  max?: number;
  /** Số chữ số lẻ: 0 cho hàng chiếc (mặc định), MATERIAL_QTY_DECIMALS cho NVL. */
  decimals?: number;
  /**
   * Trần nghiệp vụ cho ô không nằm trong Form.Item (số còn lại, số được giao…): không tự cắt, vượt thì viền đỏ và báo
   * lỗi dưới ô. Ô trong Form.Item dùng rule maxQtyRule thay cho prop này.
   */
  limit?: number;
  /** false: không thêm dấu chấm ngăn nghìn (ô mã số như mã chấm công). Mặc định true. */
  grouping?: boolean;
};

// Ghi qua setter gốc rồi phát sự kiện input: antd đọc lại chữ trong ô như khi người dùng gõ.
const setNativeValue = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')?.set;

/**
 * Chữ sắp chèn (phím vừa gõ / chữ vừa dán) làm phần nguyên dài quá `maxDigits` chữ số thì chặn và ghi vào ô
 * bản đã cắt chữ số thừa. Cần làm ở đây vì antd chỉ định dạng lại khi giá trị đổi: ô đã đủ chữ số mà gõ thêm
 * số thì chữ vẫn dài ra.
 */
const clampInsert = (
  event: React.KeyboardEvent<HTMLInputElement> | React.ClipboardEvent<HTMLInputElement>,
  inserted: string,
  decimals: number,
  maxDigits: number,
) => {
  const input = event.target;
  if (!(input instanceof HTMLInputElement)) return;
  const { value, selectionStart, selectionEnd } = input;
  const next =
    value.slice(0, selectionStart ?? value.length) +
    inserted +
    value.slice(selectionEnd ?? value.length);
  if (parseQtyInput(next, decimals, maxDigits) === parseQtyInput(next, decimals, Infinity)) return;
  event.preventDefault();
  setNativeValue?.call(input, formatQtyTyping(next, decimals, maxDigits));
  input.dispatchEvent(new Event('input', { bubbles: true }));
};

/**
 * Ô nhập số lượng dùng chung: dấu chấm ngăn nghìn hiện ngay khi gõ (1.250.000), phần lẻ sau dấu phẩy (12,5).
 * Gõ hoặc dán số vượt `max` thì cảnh báo nhưng vẫn giữ, chỉ chặn chữ số thừa; nhỏ hơn `min` thì antd sửa về `min` khi rời ô.
 * Phải chuyển tiếp props vì Form.Item truyền value / onChange vào phần tử con trực tiếp.
 */
export function QtyInput({
  min = 0,
  max = MAX_PIECE_QTY,
  decimals = 0,
  limit,
  grouping = true,
  onKeyDown,
  onPaste,
  ...props
}: QtyInputProps) {
  // Dấu chấm chỉ dùng ngăn nghìn (phần lẻ đứng sau dấu phẩy) nên bỏ dấu chấm là bỏ ngăn nghìn.
  const group = (text: string) => (grouping ? text : text.replaceAll('.', ''));
  const maxDigits = maxIntDigits(max);
  const isOver = exceedsLimit(props.value, limit);
  const isOverMax = !isOver && exceedsLimit(props.value, max);
  const field = (
    <InputNumber<number>
      {...props}
      status={isOver ? 'error' : isOverMax ? 'warning' : props.status}
      min={min}
      // Không truyền max cho antd: antd sẽ kéo số về max khi rời ô, trong khi ô cho phép gõ vượt trần.
      precision={decimals}
      formatter={(value, { userTyping, input }) =>
        group(
          userTyping
            ? formatQtyTyping(input, decimals, maxDigits)
            : formatQtyValue(value, decimals),
        )
      }
      // antd nhận chuỗi số chuẩn ("1234.5"); trả chuỗi để ô trống là rỗng chứ không thành 0.
      parser={(text) => parseQtyInput(text, decimals, maxDigits) as unknown as number}
      onKeyDown={(e) => {
        // Dấu chấm do ô tự thêm; ô số nguyên không có phần lẻ nên chặn cả dấu phẩy.
        if (e.key === '.' || (decimals === 0 && e.key === ',')) e.preventDefault();
        else if (/^\d$/.test(e.key)) clampInsert(e, e.key, decimals, maxDigits);
        onKeyDown?.(e);
      }}
      onPaste={(e) => {
        clampInsert(e, e.clipboardData.getData('text'), decimals, maxDigits);
        onPaste?.(e);
      }}
    />
  );
  if (limit === undefined && !isOverMax) return field;
  return (
    <>
      {field}
      {isOver && limit !== undefined && (
        <Typography.Text type="danger" style={{ display: 'block', fontSize: 12 }}>
          {overLimitText(limit)}
        </Typography.Text>
      )}
      {isOverMax && (
        <Typography.Text type="warning" style={{ display: 'block', fontSize: 12 }}>
          {overLimitText(max)}
        </Typography.Text>
      )}
    </>
  );
}
