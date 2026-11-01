import { InputNumber } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { formatPriceInput, formatPriceTyping, parsePriceInput } from '../utils/priceInput';

type CmtPriceInputProps = {
  value: number | null;
  onChange: (value: number | null) => void;
};

/**
 * Ô đơn giá CMT (VND): chỉ nhận số nguyên, dấu chấm ngăn nghìn do ô tự thêm nên phím `.` và `,` bị chặn.
 * Không đặt min / max / precision: antd sẽ tự sửa số khi rời ô; số sai để orderValidation báo lỗi.
 */
export function CmtPriceInput({ value, onChange }: CmtPriceInputProps) {
  return (
    <InputNumber<number>
      controls={false}
      suffix="₫"
      placeholder={MESSAGES.orders.unitPriceExample}
      style={{ width: '100%' }}
      formatter={(v, { userTyping, input }) =>
        userTyping ? formatPriceTyping(input) : formatPriceInput(v)
      }
      parser={(v) => parsePriceInput(v) as unknown as number}
      value={value}
      onChange={onChange}
      onKeyDown={(e) => {
        if (e.key === '.' || e.key === ',') e.preventDefault();
      }}
    />
  );
}
