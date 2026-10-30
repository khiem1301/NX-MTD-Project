import { AutoComplete } from 'antd';
import { useMemo } from 'react';
import { useProductColors } from '@/hooks/useOrderLookups';
import { filterColors } from '@/utils/colorSearch';

type ColorAutoCompleteProps = {
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
  /** Màu đang có trong form (chưa lưu) cũng được gợi ý. */
  extraColors?: readonly string[];
};

/** Ô màu: gợi ý các màu đã khai ở mã hàng trước (gõ không dấu cũng được), vẫn nhập được màu mới. */
export function ColorAutoComplete({ value, onChange, disabled, extraColors = [] }: ColorAutoCompleteProps) {
  const colors = useProductColors();
  const all = useMemo(() => {
    const seen = new Map<string, string>();
    for (const c of [...(colors.data ?? []), ...extraColors]) {
      const t = c.trim();
      if (t && !seen.has(t.toLowerCase())) seen.set(t.toLowerCase(), t);
    }
    return [...seen.values()];
  }, [colors.data, extraColors]);
  const options = useMemo(() => filterColors(all, value).map((c) => ({ value: c })), [all, value]);

  return (
    <AutoComplete
      value={value}
      options={options}
      disabled={disabled}
      maxLength={50}
      style={{ width: '100%' }}
      onChange={onChange}
      defaultActiveFirstOption
    />
  );
}
