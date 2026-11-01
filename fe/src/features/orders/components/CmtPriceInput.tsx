import { InputNumber } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { formatPriceInput, parsePriceInput } from '../utils/priceInput';

type CmtPriceInputProps = {
  value: number | null;
  onChange: (value: number | null) => void;
};

/**
 * Ô đơn giá CMT: đang gõ giữ nguyên chữ người dùng nhập, rời ô mới định dạng lại.
 * Không đặt min / max / precision: antd sẽ tự sửa số khi rời ô; số sai để orderValidation báo lỗi.
 */
export function CmtPriceInput({ value, onChange }: CmtPriceInputProps) {
  return (
    <InputNumber<number>
      controls={false}
      suffix="₫"
      placeholder={MESSAGES.orders.unitPriceExample}
      style={{ width: '100%' }}
      formatter={(v, { userTyping, input }) => (userTyping ? input : formatPriceInput(v))}
      parser={(v) => parsePriceInput(v) as unknown as number}
      value={value}
      onChange={onChange}
    />
  );
}
