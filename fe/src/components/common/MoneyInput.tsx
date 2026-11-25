import { InputNumber, type InputNumberProps } from 'antd';
import {
  formatMoneyTyping,
  formatMoneyValue,
  MONEY_MAX_DIGITS,
  parseMoneyInput,
} from '@/utils/moneyInput';

export type MoneyInputProps = Omit<
  InputNumberProps<number>,
  'formatter' | 'parser' | 'precision' | 'stringMode'
> & {
  /** Nhận số âm (giá trị chốt, số tiền trả khách). Mặc định không. */
  allowNegative?: boolean;
  /** Số chữ số tối đa của phần nguyên, mặc định 13 (hơn 1.000 tỷ). */
  maxDigits?: number;
  /** false: không hiện ký hiệu ₫ sau số. Mặc định true. */
  currency?: boolean;
};

/**
 * Ô nhập tiền VND dùng chung: số nguyên, dấu chấm ngăn nghìn hiện ngay khi gõ (1.250.000), phím `.` và `,` bị chặn vì dấu chấm do ô tự
 * thêm. Phải chuyển tiếp props vì Form.Item truyền value / onChange vào phần tử con trực tiếp.
 */
export function MoneyInput({
  allowNegative = false,
  maxDigits = MONEY_MAX_DIGITS,
  currency = true,
  min,
  onKeyDown,
  ...props
}: MoneyInputProps) {
  return (
    <InputNumber<number>
      {...props}
      min={min ?? (allowNegative ? undefined : 0)}
      addonAfter={currency ? '₫' : undefined}
      formatter={(value, { userTyping, input }) =>
        userTyping ? formatMoneyTyping(input, allowNegative, maxDigits) : formatMoneyValue(value)
      }
      // Trả chuỗi để ô trống là rỗng chứ không thành 0.
      parser={(text) => parseMoneyInput(text, allowNegative, maxDigits) as unknown as number}
      onKeyDown={(e) => {
        if (e.key === '.' || e.key === ',' || (!allowNegative && e.key === '-')) e.preventDefault();
        onKeyDown?.(e);
      }}
    />
  );
}
