import { InputNumber } from 'antd';
import { AI_LABELS, AI_LOOKBACK } from '../constants';

type LookbackDaysInputProps = {
  value: number;
  disabled?: boolean;
  onChange: (days: number) => void;
};

/** Tổ trưởng chọn số ngày gần nhất lấy số liệu trước khi tạo / tạo lại gợi ý AI. */
export function LookbackDaysInput({ value, disabled, onChange }: LookbackDaysInputProps) {
  return (
    <InputNumber
      aria-label={AI_LABELS.lookbackBefore}
      addonBefore={AI_LABELS.lookbackBefore}
      addonAfter={AI_LABELS.lookbackAfter}
      min={AI_LOOKBACK.MIN}
      max={AI_LOOKBACK.MAX}
      precision={0}
      value={value}
      disabled={disabled}
      onChange={(days) => days !== null && onChange(days)}
      style={{ width: 260 }}
    />
  );
}
