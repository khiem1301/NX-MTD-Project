import { AutoComplete } from 'antd';
import { useMemo } from 'react';
import { useProductColors } from '@/hooks/useOrderLookups';
import { filterColors } from '@/utils/colorSearch';
import { COLOR_MAX_LENGTH } from '../constants';

type ColorAutoCompleteProps = {
  /** value / onChange để trống khi đặt trong Form.Item (Form.Item tự truyền vào). */
  value?: string;
  onChange?: (value: string) => void;
  disabled?: boolean;
  size?: 'small' | 'middle' | 'large';
  status?: 'error';
  placeholder?: string;
  /** Màu đang có trong form (chưa lưu) cũng được gợi ý. */
  extraColors?: readonly string[];
  /**
   * DEV-ALL-132: mã hàng của dòng. Số → chỉ gợi ý màu đã có của mã đó; `null` → mã mới chưa có trong hệ thống, không gợi
   * ý màu nào; bỏ trống → màu của mọi mã hàng.
   */
  productId?: number | null;
};

/** Ô màu: gợi ý màu (gõ không dấu cũng được), vẫn nhập được màu mới. */
export function ColorAutoComplete({
  value = '',
  onChange,
  disabled,
  size,
  status,
  placeholder,
  extraColors = [],
  productId,
}: ColorAutoCompleteProps) {
  const colors = useProductColors(productId);
  const all = useMemo(() => {
    const seen = new Map<string, string>();
    const source = productId === null ? [] : [...(colors.data ?? []), ...extraColors];
    for (const c of source) {
      const t = c.trim();
      if (t && !seen.has(t.toLowerCase())) seen.set(t.toLowerCase(), t);
    }
    return [...seen.values()];
  }, [colors.data, extraColors, productId]);
  const options = useMemo(() => filterColors(all, value).map((c) => ({ value: c })), [all, value]);

  return (
    <AutoComplete
      value={value}
      options={options}
      disabled={disabled}
      size={size}
      status={status}
      placeholder={placeholder}
      maxLength={COLOR_MAX_LENGTH}
      style={{ width: '100%' }}
      onChange={onChange}
      defaultActiveFirstOption
    />
  );
}
